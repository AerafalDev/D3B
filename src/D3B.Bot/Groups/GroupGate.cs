using System.Globalization;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>A master's gate: before it acts, every follower with it and in its party; until then it invites them and waits (D431, D432, D437).</summary>
/// <remarks>
/// <para>
/// One per bot, which holds nothing while nobody follows it. The requests it holds are those that start an action out of a
/// fight, but its walks and map changes, which its followers copy as they come (D447, the owner's choice): a use, an NPC
/// talked to, a zaap, the haven bag and the attack D3B sends. So the master moves on at once, and waits before an attack,
/// a harvest, a zaap, the haven bag or a door, whatever its profile, and, a member missing, where it stands.
/// </para>
/// <para>
/// It waits too for each follower's life to be over the share its own profile heals under, before any action it holds, as
/// its own activity heals before each step (D433, D441); each follower heals by itself.
/// </para>
/// <para>
/// It waits for a follower fighting as long as it takes, as a fight ends by itself; for a follower missing otherwise,
/// disconnected, paused, on its way, stuck or out of its party, five minutes at most, then goes on without it and says so in
/// its journal and the host's log, the owner's choice (D487): measured on the owner's captures of 2026-11-19 (D464), a master
/// waited 30 minutes for a follower the server held until the server closed every connection of the group. A follower left
/// behind still comes after its master, and is waited for again once it is with it. The bot's card says whom it waits for
/// (<see cref="BotActor.WaitingFor"/>).
/// </para>
/// <para>
/// The party: the master invites each follower connected and not in its party by its name (D431), again once the
/// follower's connection is a new one, and every 10 s while the invitation is not answered, the owner's choice. A party
/// another leads it keeps while that party holds each of its followers, such as one formed by hand that a follower leads, the
/// owner's ask (D471); else it leaves it first, as its followers leave a party it is not in: an invitation into a party
/// another leads is not measured.
/// </para>
/// <para>
/// In a group the game follows (<see cref="GameFollowing"/>, D476), it asks its members to follow it while a follower with it
/// does not, every 30 s at most: the owner's report, its "follow me" turned off stops its members, turned on again makes them
/// follow.
/// </para>
/// </remarks>
internal sealed class GroupGate : IActionGate
{
    /// <summary>How long an invitation goes unanswered before the master sends it again, the owner's choice (D437).</summary>
    public static readonly TimeSpan InviteAgainAfter = TimeSpan.FromSeconds(10);

    /// <summary>How long the master waits before it asks its members to follow it again, while one with it does not (D476).</summary>
    public static readonly TimeSpan CallAgainAfter = TimeSpan.FromSeconds(30);

    /// <summary>How long the master waits for a follower missing, but fighting or healing, before it goes on without it, the owner's choice (D487).</summary>
    public static readonly TimeSpan LeaveBehindAfter = TimeSpan.FromMinutes(5);

    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;

    // Only the bot's behaviors, one at a time, read and write them.
    private readonly Dictionary<CharacterKey, Invitation> _invited = [];
    private readonly Dictionary<CharacterKey, long?> _behind = [];
    private (int Party, DateTimeOffset At)? _left;
    private (string? Connection, int Party)? _optionsOff;
    private (int Party, DateTimeOffset At)? _called;

    public GroupGate(BotActor bot, GroupDirectory groups)
    {
        _bot = bot;
        _groups = groups;
    }

    /// <summary>Tells whether a request starts an action the master waits for its followers before.</summary>
    /// <param name="request">The request.</param>
    /// <returns>Whether the gate holds it.</returns>
    public static bool Holds(IMessage request)
    {
        return request is InteractiveUseRequest or NpcActionRequest or TeleportRequest or HavenBagEnterRequest or AttackMonsterRequest;
    }

    private static PartyInvitationRequest Invite(string name)
    {
        return new PartyInvitationRequest
        {
            Type = PartyType.Classical,
            Target = new PlayerSearch { ByName = new PlayerSearch.Types.ByName { Name = name } },
        };
    }

    private static DateTimeOffset? Earliest(DateTimeOffset? one, DateTimeOffset? other)
    {
        return one is { } first && (other is not { } second || first < second) ? first : other;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A request it does not hold, such as a walk, first has the master invite its followers out of its party, waiting for
    /// nothing: the owner's report from game (D457), the master walked six maps before its first door, where it invited.
    /// </remarks>
    public Task PassAsync(BotContext context, IMessage request, CancellationToken cancellationToken)
    {
        return Holds(request) ? GatherAsync(context, cancellationToken) : InviteAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Until every follower is with the master, in its party and its life over the share; at once for a master in a fight or followed by nobody.</remarks>
    public async Task GatherAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_bot.State.IsFighting || _groups.FollowersOf(_bot.Key).Count is 0)
            return;

        IReadOnlyList<MissingMember> said = [];
        var since = new Dictionary<CharacterKey, DateTimeOffset>();

        try
        {
            while (true)
            {
                // Seen before anything is decided from them, so that what changes after wakes the wait below.
                var changed = _groups.Changed;
                var followers = _groups.FollowersOf(_bot.Key);
                var seen = followers.Select(BotSeen.Of).ToList();
                var state = _bot.State;

                if (followers.Count is 0 || state.IsFighting)
                    return;

                var left = await LeaveOthersAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
                var invited = await InviteAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
                var called = await CallAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
                var (missing, due) = LeaveBehind(Regroup(state, followers), since);
                var healed = (DateTimeOffset?)null;

                // Each follower's life over the share the master's profile heals under, before any action, as an activity heals
                // before its step (D433, D441); before an attack, the master waits before its walk to the group of monsters,
                // which leaves its cell meanwhile (D447).
                if (missing.Count is 0 && GroupLife.ShareOf(_bot) is { } share)
                    (missing, healed) = Healing(state, [.. followers.Where(follower => !_behind.ContainsKey(follower.Key))], share);

                if (missing.Count is 0)
                {
                    if (said.Count > 0)
                        GroupLog.Gathered(_bot.Log, _bot.Key.CharacterId);

                    return;
                }

                if (!missing.SequenceEqual(said))
                {
                    said = missing;
                    _bot.WaitFor(missing);

                    if (_bot.Log.IsEnabled(LogLevel.Information))
                    {
                        var names = string.Join(", ", missing.Select(member => $"{member.Key.CharacterId} {member.Absence}"));

                        GroupLog.Waiting(_bot.Log, _bot.Key.CharacterId, names);
                    }
                }

                await ChangedAsync(changed, state, seen, Earliest(Earliest(Earliest(Earliest(invited, left), healed), called), due), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _bot.WaitFor([]);
        }
    }

    /// <summary>Tells whether the master went on without a follower, which it waits for again once it is back (D487).</summary>
    /// <param name="follower">The follower's character.</param>
    /// <returns>Whether it is left behind.</returns>
    internal bool IsBehind(CharacterKey follower)
    {
        return _behind.ContainsKey(follower);
    }

    // The followers missing; a follower left behind found with the master, or on another map than the one it was left on, is
    // waited for again (D487): one that catches its master up as it walks on, one map behind, is on its way again.
    private IReadOnlyList<MissingMember> Regroup(CharacterState state, IReadOnlyList<BotActor> followers)
    {
        var missing = GroupChain.Missing(state, followers);

        foreach (var (key, map) in _behind.ToList())
        {
            if (!missing.Any(member => member.Key == key) || followers.FirstOrDefault(follower => follower.Key == key) is not { } follower || follower.State.MapId != map)
                _behind.Remove(key);
        }

        return missing;
    }

    // The members the master still waits for, and when the first of them has been missing for the bound: a follower missing
    // but in a fight for five minutes of this wait is left behind (D487). A fight starts its count again, as it ends by itself.
    private (IReadOnlyList<MissingMember> Missing, DateTimeOffset? Due) LeaveBehind(IReadOnlyList<MissingMember> missing, Dictionary<CharacterKey, DateTimeOffset> since)
    {
        var now = _bot.Time.GetUtcNow();
        var kept = new List<MissingMember>();
        DateTimeOffset? due = null;

        foreach (var key in since.Keys.Where(key => !missing.Any(member => member.Key == key && member.Absence is not MemberAbsence.Fighting)).ToList())
            since.Remove(key);

        foreach (var member in missing)
        {
            if (_behind.ContainsKey(member.Key))
                continue;

            if (member.Absence is MemberAbsence.Fighting)
            {
                kept.Add(member);

                continue;
            }

            if (!since.TryGetValue(member.Key, out var first))
                since[member.Key] = first = now;

            if (now - first < LeaveBehindAfter)
            {
                kept.Add(member);
                due = Earliest(due, first + LeaveBehindAfter);

                continue;
            }

            _behind[member.Key] = _groups.FollowersOf(_bot.Key).FirstOrDefault(follower => follower.Key == member.Key)?.State.MapId;
            GroupLog.LeftBehind(_bot.Log, _bot.Key.CharacterId, member.Key.CharacterId, member.Absence);

            var culture = CultureInfo.CurrentUICulture;
            var text = string.Format(culture, GroupTexts.Text("LeftBehind").In(culture), member.Name);

            _bot.Journal.Add(new NoteEntry(now, _bot.Activity?.Activity.Name ?? string.Empty, text));
        }

        return (kept, due);
    }

    // The party kept as the master goes, outside a fight: a party another leads left, its followers invited.
    private async Task InviteAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = _bot.State;
        var followers = _groups.FollowersOf(_bot.Key);

        if (followers.Count is 0 || state.IsFighting)
            return;

        Regroup(state, followers);
        await LeaveOthersAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
        await InviteAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
        await CallAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
    }

    // Gives the next time the master may ask again, if it asked its members to follow it. In a group the game follows (D476),
    // a follower with it in its party that does not follow it: measured on the owner's game of 2026-11-20, the leader's ask
    // turned on the following of each member whose own option was on, and its "stop" turned it off, the owner's report too.
    private async Task<DateTimeOffset?> CallAsync(BotContext context, CharacterState state, IReadOnlyList<BotActor> followers, CancellationToken cancellationToken)
    {
        var now = _bot.Time.GetUtcNow();

        if (state.Party is not { } party || !GameFollowing.Applies(state, [.. followers.Select(follower => follower.State)], now))
            return null;

        var lagging = followers.Any(follower => follower.IsConnected && party.Members.Contains(follower.Key.CharacterId)
            && GroupChain.Together(state, follower.State) && follower.State.Party?.FollowsLeader != true);

        if (!lagging)
            return null;

        if (_called is { } sent && sent.Party == party.Id && now - sent.At < CallAgainAfter)
            return sent.At + CallAgainAfter;

        _called = (party.Id, now);
        GroupLog.CallingFollowers(_bot.Log, _bot.Key.CharacterId, party.Id);
        await context.SendNowAsync(new PartyFollowMeRequest { Follow = true }, cancellationToken).ConfigureAwait(false);

        return now + CallAgainAfter;
    }

    // The followers whose life is under the share, in the chain's order, and when the first regenerates to it: its life rises
    // with no message, by the bot's reckoning (D153), so that the wait looks again then. Each follower heals by itself.
    private (IReadOnlyList<MissingMember> Missing, DateTimeOffset? Healed) Healing(CharacterState state, IReadOnlyList<BotActor> followers, int share)
    {
        var now = _bot.Time.GetUtcNow();
        var missing = new List<MissingMember>();
        DateTimeOffset? healed = null;

        foreach (var follower in GroupChain.Order(state, followers))
        {
            if (GroupLife.Lacking(follower, share, now) is var lacking && lacking <= TimeSpan.Zero)
                continue;

            missing.Add(new MissingMember(follower.Key, follower.State.Name, MemberAbsence.Healing));
            healed = Earliest(healed, now + lacking);
        }

        return (missing, healed);
    }

    // Gives the next time the leaving may go again, while the master is in a party another leads that lacks a follower: a
    // leaving the server answered nothing to goes again, as an invitation does. A party another leads that it keeps, its
    // automatic options are turned off as its followers' are (D437, D471): joining its leader's fights would take the master
    // into a fight of its follower's, where its own followers are not told to join.
    private async Task<DateTimeOffset?> LeaveOthersAsync(BotContext context, CharacterState state, IReadOnlyList<BotActor> followers, CancellationToken cancellationToken)
    {
        if (state.Party is not { } party || party.LeaderId == state.Key.CharacterId)
            return null;

        if (GroupChain.HoldsAll(state, followers))
        {
            if (_optionsOff != (_bot.ConnectionId, party.Id))
            {
                _optionsOff = (_bot.ConnectionId, party.Id);
                GroupLog.OptionsOff(_bot.Log, _bot.Key.CharacterId, party.Id);
                await context.SendNowAsync(new PartyAutoOptionOneOffRequest(), cancellationToken).ConfigureAwait(false);
                await context.SendNowAsync(new PartyAutoOptionTwoOffRequest(), cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        var now = _bot.Time.GetUtcNow();

        if (_left is { } sent && sent.Party == party.Id && now - sent.At < InviteAgainAfter)
            return sent.At + InviteAgainAfter;

        _left = (party.Id, now);
        GroupLog.Leaving(_bot.Log, _bot.Key.CharacterId, party.Id);
        await context.SendNowAsync(new PartyLeaveRequest { PartyId = party.Id }, cancellationToken).ConfigureAwait(false);

        return now + InviteAgainAfter;
    }

    // Gives the next time an invitation may go again, if one waits for an answer.
    private async Task<DateTimeOffset?> InviteAsync(BotContext context, CharacterState state, IReadOnlyList<BotActor> followers, CancellationToken cancellationToken)
    {
        var self = state.Key.CharacterId;

        // In a party another leads, the invitations would be into that party: the master leaves it first.
        if (state.Party is { } party && party.LeaderId != self)
            return null;

        DateTimeOffset? next = null;

        foreach (var follower in followers)
        {
            var theirs = follower.State;

            if (!follower.IsConnected || theirs.Name.Length is 0 || state.Party?.Members.Contains(follower.Key.CharacterId) == true || theirs.Party?.LeaderId == self)
                continue;

            var now = _bot.Time.GetUtcNow();

            if (_invited.TryGetValue(follower.Key, out var sent) && sent.Connection == follower.ConnectionId && now - sent.At < InviteAgainAfter)
            {
                next = Earliest(next, sent.At + InviteAgainAfter);

                continue;
            }

            _invited[follower.Key] = new Invitation(follower.ConnectionId, now);
            next = Earliest(next, now + InviteAgainAfter);
            GroupLog.Inviting(_bot.Log, self, follower.Key.CharacterId);

            await context.SendNowAsync(Invite(theirs.Name), cancellationToken).ConfigureAwait(false);
        }

        return next;
    }

    private Task ChangedAsync(Task changed, CharacterState state, IReadOnlyList<BotSeen> followers, DateTimeOffset? inviteAgain, CancellationToken cancellationToken)
    {
        var party = state.Party;

        // The master's own party and fight change what it waits for; its other changes, such as others walking on its map, do not.
        (BotActor, Func<BotActor, bool>) own = (_bot, bot => bot.State.Party != party || bot.State.IsFighting);

        return GroupWaits.AnyAsync([.. followers.Select(seen => (seen.Bot, (Func<BotActor, bool>)seen.Changed)), own], changed, _bot.Time, inviteAgain - _bot.Time.GetUtcNow(), cancellationToken);
    }

    private sealed record Invitation(string? Connection, DateTimeOffset At);
}
