using D3B.Bot.Characters;
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
/// It waits as long as it takes, with no delay (D87): a follower disconnected, paused, fighting, on its way or stuck. The
/// bot's card says whom it waits for (<see cref="BotActor.WaitingFor"/>).
/// </para>
/// <para>
/// The party: the master invites each follower connected and not in its party by its name (D431), again once the
/// follower's connection is a new one, and every 10 s while the invitation is not answered, the owner's choice. A party
/// another leads, it leaves first, as its followers do.
/// </para>
/// </remarks>
internal sealed class GroupGate : IActionGate
{
    /// <summary>How long an invitation goes unanswered before the master sends it again, the owner's choice (D437).</summary>
    public static readonly TimeSpan InviteAgainAfter = TimeSpan.FromSeconds(10);

    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;

    // Only the bot's behaviors, one at a time, read and write them.
    private readonly Dictionary<CharacterKey, Invitation> _invited = [];
    private (int Party, DateTimeOffset At)? _left;

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

                var left = await LeaveOthersAsync(context, state, cancellationToken).ConfigureAwait(false);
                var invited = await InviteAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
                var missing = GroupChain.Missing(state, followers);
                var healed = (DateTimeOffset?)null;

                // Each follower's life over the share the master's profile heals under, before any action, as an activity heals
                // before its step (D433, D441); before an attack, the master waits before its walk to the group of monsters,
                // which leaves its cell meanwhile (D447).
                if (missing.Count is 0 && GroupLife.ShareOf(_bot) is { } share)
                    (missing, healed) = Healing(state, followers, share);

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

                await ChangedAsync(changed, state, seen, Earliest(Earliest(invited, left), healed), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _bot.WaitFor([]);
        }
    }

    // The party kept as the master goes, outside a fight: a party another leads left, its followers invited.
    private async Task InviteAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = _bot.State;
        var followers = _groups.FollowersOf(_bot.Key);

        if (followers.Count is 0 || state.IsFighting)
            return;

        await LeaveOthersAsync(context, state, cancellationToken).ConfigureAwait(false);
        await InviteAsync(context, state, followers, cancellationToken).ConfigureAwait(false);
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

    // Gives the next time the leaving may go again, while the master is in a party another leads: a leaving the server
    // answered nothing to goes again, as an invitation does.
    private async Task<DateTimeOffset?> LeaveOthersAsync(BotContext context, CharacterState state, CancellationToken cancellationToken)
    {
        if (state.Party is not { } party || party.LeaderId == state.Key.CharacterId)
            return null;

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
