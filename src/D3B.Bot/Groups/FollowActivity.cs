using System.Collections.Frozen;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Groups;

/// <summary>Following a master: in its party, map after map, in the chain's order (D430, D431, D432, D436).</summary>
/// <remarks>
/// <para>
/// The follower joins its master's group as it starts (<see cref="GroupDirectory"/>), so that the master, whatever its
/// profile, waits for it before acting (<see cref="GroupGate"/>). Each step looks at the state, as every activity does (D87):
/// </para>
/// <list type="number">
/// <item>The party first (D431): a party another leads is left, the master's invitation accepted and any other refused,
/// and once in the master's party, the game's two automatic options turned off. Measured on 3.7.14.18 (D436): the client
/// turns them on by itself 3 to 22 ms after each party it joins, as long as its own window has them on, so the follower
/// lets the client's display time pass, then turns both off, at each party joined.</item>
/// <item>The master missing, disconnected or paused, the follower waits where it stands: no follower leads (D432).</item>
/// <item>With its master, it waits for the master to move.</item>
/// <item>Else it follows the chain: the member before it on its map, it waits for that one to go; then it goes to the
/// master's map by the world's paths, which from the map the master left is the way the master took, and from afar its
/// own journey. As the master enters its haven bag, each follower enters its own, and leaves it as the master leaves
/// his, the owner's choice.</item>
/// </list>
/// <para>
/// The AI plays the follower's own fights; joining its master's is 10c's (D431).
/// </para>
/// </remarks>
public sealed class FollowActivity : BotActivity, IDisposable
{
    // A wait no message ends, such as a master never connected, looks again at least this often.
    private static readonly TimeSpan _lookAgain = TimeSpan.FromSeconds(5);

    private readonly Bots _bots;
    private readonly string _masterId;
    private readonly Lock _joining = new();

    // Only the activity's steps, one at a time, read and write them.
    private readonly HashSet<int> _refused = [];
    private (string? Connection, int Party)? _optionsOff;

    private volatile IDisposable? _membership;
    private volatile ActivityReport _report = new() { Doing = GameText.Of(GroupTexts.Text("DoingStart")) };

    /// <inheritdoc/>
    public override string Name => "Follow";

    /// <inheritdoc/>
    public override ActivityReport? Report => _report;

    /// <inheritdoc/>
    /// <remarks>The AI plays the fights the follower meets on its way, such as an aggression; its master's are 10c's to join.</remarks>
    public override bool FightsItsFights => true;

    /// <summary>Initializes a new instance of the <see cref="FollowActivity"/> class.</summary>
    /// <param name="settings">The activity's settings: the master.</param>
    /// <param name="bots">The host's bots, the master's among them, and their groups.</param>
    public FollowActivity(FollowSettings settings, Bots bots)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bots);

        _masterId = settings.Master;
        _bots = bots;
    }

    /// <summary>Gets the master a follower's settings name.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The master's character.</returns>
    /// <exception cref="ActivityException">No master is named.</exception>
    public static CharacterKey MasterOf(FollowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return CharacterKey.TryParseId(settings.Master, out var master)
            ? master
            : throw new ActivityException("No master is chosen.", GameText.Of(GroupTexts.Text("FailNoMaster")));
    }

    private static int IndexOf(IReadOnlyList<BotActor> chain, BotActor bot)
    {
        for (var at = 0; at < chain.Count; at++)
        {
            if (chain[at] == bot)
                return at;
        }

        return -1;
    }

    private static ActivityException Refused(FollowRefusal refusal)
    {
        return new ActivityException($"The character cannot follow its master: {refusal}.", GameText.Of(GroupTexts.Text($"Fail{refusal}")));
    }

    // Until the master or the one before it moves, connects, pauses or resumes, the follower's own party or map changes, or the group does.
    private static Task WaitAsync(BotContext context, Task changed, BotSeen mine, BotSeen? leader, BotSeen? before, CancellationToken cancellationToken)
    {
        var conditions = new List<(BotActor, Func<BotActor, bool>)> { (mine.Bot, mine.Moved) };

        if (leader is not null)
            conditions.Add((leader.Bot, leader.Moved));

        if (before is not null)
            conditions.Add((before.Bot, before.Moved));

        return GroupWaits.AnyAsync(conditions, changed, context.Time, _lookAgain, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var me = context.Actor;
        var master = Join(me);

        if (await KeepPartyAsync(context, master, cancellationToken).ConfigureAwait(false))
            return true;

        // Seen before anything is decided from them, so that what changes after wakes the waits below.
        var changed = _bots.Groups.Changed;
        var mine = BotSeen.Of(me);
        var state = mine.State;

        var known = _bots.TryGet(master, out var leader) ? BotSeen.Of(leader) : null;

        if (known is not { IsConnected: true, IsPaused: false } seen)
        {
            Publish(known is null ? GameText.Of(GroupTexts.Text("DoingNoMaster"))
                : GameText.Of(GroupTexts.Text(known.IsConnected ? "DoingMasterPaused" : "DoingMasterDisconnected"), GameTextValue.Of(known.State.Name)));
            await WaitAsync(context, changed, mine, known, null, cancellationToken).ConfigureAwait(false);

            return true;
        }

        var theirs = seen.State;
        var name = GameTextValue.Of(theirs.Name);

        if (GroupChain.Together(theirs, state))
        {
            Publish(GameText.Of(GroupTexts.Text("DoingFollows"), name));
            await WaitAsync(context, changed, mine, seen, null, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // The master out of its haven bag, the follower leaves its own, back where it entered it, as the master did.
        if (GroupChain.IsInOwnHavenBag(state))
        {
            Publish(GameText.Of(GroupTexts.Text("DoingJoins"), name));

            if (await context.LeaveHavenBagAsync(cancellationToken).ConfigureAwait(false) is not MapChangeOutcome.Changed)
                await StuckAsync(context, seen, name, cancellationToken).ConfigureAwait(false);

            return true;
        }

        var chain = GroupChain.Order(theirs, _bots.Groups.FollowersOf(master));
        var at = IndexOf(chain, me);

        // The one before it goes first, from the map they share, as long as it is not with the master yet (D432).
        if (at > 0 && BotSeen.Of(chain[at - 1]) is var before && before.State.MapId == state.MapId && !GroupChain.Together(theirs, before.State))
        {
            Publish(GameText.Of(GroupTexts.Text("DoingBehind"), GameTextValue.Of(before.State.Name)));
            await WaitAsync(context, changed, mine, seen, before, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (GroupChain.IsInOwnHavenBag(theirs))
        {
            Publish(GameText.Of(GroupTexts.Text("DoingFollows"), name));

            // A map the server refuses the haven bag on, such as a dungeon's, is no reason to ask again at once.
            if (await context.EnterHavenBagAsync(cancellationToken).ConfigureAwait(false) is not MapChangeOutcome.Changed)
                await StuckAsync(context, seen, name, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (theirs.MapId is not { } map)
        {
            await WaitAsync(context, changed, mine, seen, null, cancellationToken).ConfigureAwait(false);

            return true;
        }

        Publish(GameText.Of(GroupTexts.Text("DoingJoins"), name));

        if (await context.TravelAsync(FrozenSet.Create(map), cancellationToken).ConfigureAwait(false) is not TravelOutcome.Arrived)
            await StuckAsync(context, seen, name, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _membership?.Dispose();
        _membership = null;
    }

    private CharacterKey Join(BotActor me)
    {
        var master = MasterOf(new FollowSettings { Master = _masterId });

        if (_membership is not null)
            return master;

        lock (_joining)
        {
            if (_membership is null)
                _membership = _bots.Groups.Follow(me, master, out var refusal) ?? throw Refused(refusal);
        }

        return master;
    }

    // Whether it sent something of the party's: the step is over, and the next looks at the state again.
    private async Task<bool> KeepPartyAsync(BotContext context, CharacterKey master, CancellationToken cancellationToken)
    {
        var state = context.State;
        var me = context.Actor;
        var masterId = master.CharacterId;

        // A party another leads is left first, whoever invites (D431).
        if (state.Party is { } other && other.LeaderId != masterId)
        {
            Publish(GameText.Of(GroupTexts.Text("DoingLeaves")));
            GroupLog.Leaving(context.Log, me.Key.CharacterId, other.Id);
            await context.SendAsync(new PartyLeaveRequest { PartyId = other.Id }, cancellationToken).ConfigureAwait(false);
            await context.WaitUntilAsync(now => now.Party?.Id != other.Id, context.Delays.Answer, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // D3B's own answer is no message its bot sees (D62): a refused invitation stays in the state, which it refuses once.
        if (state.Invitation is { } invitation && !_refused.Contains(invitation.PartyId) && state.Party?.Id != invitation.PartyId)
        {
            if (invitation.FromId == masterId)
            {
                Publish(GameText.Of(GroupTexts.Text("DoingParty"), GameTextValue.Of(invitation.FromName)));
                GroupLog.Accepting(context.Log, me.Key.CharacterId, invitation.PartyId);
                await context.SendAsync(new PartyInvitationAcceptRequest { PartyId = invitation.PartyId }, cancellationToken).ConfigureAwait(false);
                await context.WaitUntilAsync(now => now.Party?.Id == invitation.PartyId, context.Delays.Answer, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _refused.Add(invitation.PartyId);
                GroupLog.Refusing(context.Log, me.Key.CharacterId, invitation.FromId, invitation.PartyId);
                await context.SendAsync(new PartyInvitationRefuseRequest { PartyId = invitation.PartyId }, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        if (state.Party is { } party && party.LeaderId == masterId && _optionsOff != (me.ConnectionId, party.Id))
        {
            // Measured (D436): the client turns both on 3 to 22 ms after the party's join, which its display time leaves behind.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
            GroupLog.OptionsOff(context.Log, me.Key.CharacterId, party.Id);
            await context.SendAsync(new PartyAutoOptionOneOffRequest(), cancellationToken).ConfigureAwait(false);
            await context.SendAsync(new PartyAutoOptionTwoOffRequest(), cancellationToken).ConfigureAwait(false);
            _optionsOff = (me.ConnectionId, party.Id);

            return true;
        }

        return false;
    }

    // Stuck: the group waits for it (D432), and it tries again once its master moved, or after a while.
    private Task StuckAsync(BotContext context, BotSeen master, GameTextValue name, CancellationToken cancellationToken)
    {
        Publish(GameText.Of(GroupTexts.Text("DoingStuck"), name));

        return WaitAsync(context, _bots.Groups.Changed, BotSeen.Of(context.Actor), master, null, cancellationToken);
    }

    private void Publish(GameText doing)
    {
        _report = new ActivityReport { Doing = doing };
    }
}
