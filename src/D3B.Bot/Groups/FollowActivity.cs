using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Inventory;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Groups;

/// <summary>Following a master: in its party, in real time, walk after walk and map after map (D430, D431, D437, D447).</summary>
/// <remarks>
/// <para>
/// The follower joins its master's group as it starts (<see cref="GroupDirectory"/>), so that the master, whatever its
/// profile, waits for it before acting (<see cref="GroupGate"/>). Each step looks at the state, as every activity does (D87):
/// </para>
/// <list type="number">
/// <item>The party first (D431): a party another leads is left, the master's invitation accepted and any other refused,
/// and once in the master's party, the game's two automatic options turned off. Measured on 3.7.14.18 (D437): the client
/// turns them on by itself 3 to 22 ms after each party it joins, as long as its own window has them on, so the follower
/// lets the client's display time pass, then turns both off, at each party joined.</item>
/// <item>The master missing, disconnected or paused, the follower waits where it stands: no follower leads (D432).</item>
/// <item>With its master, its master taking its objects at the bank, it gives them by an exchange (D433, D442).</item>
/// <item>With its master, under the share of life its master's profile heals under, it heals: it eats until its life is
/// whole the food it carries that only heals, else regenerates up to the share, its master waiting to attack (D441).</item>
/// <item>With its master, as the master walks, it walks to the cell its master walks to, at once, its exit to the next map
/// as any other (D447, the owner's choice); else it waits for the master to move.</item>
/// <item>Else it changes maps towards its master, one change a step by the world's paths, which from the cell its master
/// left by is the way the master took; whatever the others' order (D447). A follower that did not copy its master's walk,
/// such as one connected again, leaves once the master is placed on its new map (D437). A change failing is tried again by
/// the same way, a journey going round only after several. As the master enters its haven bag, each follower enters its
/// own, and leaves it as the master leaves his, the owner's choice.</item>
/// </list>
/// <para>
/// As its master attacks on its map, the follower joins the fight by request (D431, D435); the AI places it and says it
/// is ready, as it plays the follower's own fights, such as an aggression on its way.
/// </para>
/// </remarks>
public sealed class FollowActivity : BotActivity, IDisposable
{
    // The changes failed in a row by the way to the master's map before a whole journey goes round, as one does (D74).
    private const int MostFailedChanges = 3;

    // A wait no message ends, such as a master never connected, looks again at least this often.
    private static readonly TimeSpan _lookAgain = TimeSpan.FromSeconds(5);

    // The owner's choice (D437): an answer no sooner than the owner's own fastest in 10a's captures, 1.28 s. Measured in
    // game, it does not close the client's window for the invitation, which an answer 0.51 s after did not either: the
    // client closes it on the player's click alone.
    private static readonly TimeSpan _answerAfter = TimeSpan.FromSeconds(1.3);

    // How long the follower waits for its exchange with its master to open, then to conclude, as the master waits (D442).
    private static readonly TimeSpan _exchangeWait = TimeSpan.FromSeconds(30);

    // Measured on the owner's test of 2026-11-19 (D458): the server took no readiness sent 0.5 s after the exchange's last
    // change, twice, and every one sent 3.5 to 4 s after; the owner's own hand waits 3 to 5 s. Unanswered, it goes again.
    private static readonly TimeSpan _readyAfterChange = TimeSpan.FromSeconds(3.5);

    private static readonly TimeSpan _readyAgainAfter = TimeSpan.FromSeconds(1);

    private readonly Bots _bots;
    private readonly GameTables _tables;
    private readonly string _masterId;
    private readonly Lock _joining = new();

    // Only the activity's steps, one at a time, read and write them.
    private readonly HashSet<int> _refused = [];
    private readonly HashSet<int> _unusable = [];
    private (string? Connection, int Party)? _optionsOff;
    private (string? Connection, int Fight)? _joined;
    private int? _over;
    private (string? Connection, int Round)? _giving;
    private ImmutableArray<int> _copied;
    private (long Map, int MastersEnd, int Cell)? _copiedTo;
    private int _failedChanges;
    private bool _healing;

    private volatile IDisposable? _membership;
    private volatile ActivityReport _report = new() { Doing = GameText.Of(GroupTexts.Text("DoingStart")) };

    /// <inheritdoc/>
    public override string Name => "Follow";

    /// <inheritdoc/>
    public override ActivityReport? Report => _report;

    /// <inheritdoc/>
    /// <remarks>The AI plays its master's fights it joins, and those it meets on its way, such as an aggression.</remarks>
    public override bool FightsItsFights => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Its master's profile's, the owner's choice (D446): measured on the owner's test of 2026-11-18, each character's client
    /// sends its own choice as it joins, the bonus each character's own, so the followers got the experience their clients
    /// chose while their master got the drops. The client's while the master runs no profile, or one whose AI does not fight
    /// (D438), and while the master follows: no follower leads (D432), and two followers naming each other would ask each other
    /// for ever.
    /// </remarks>
    public override ChallengeReward ChallengeReward
        => CharacterKey.TryParseId(_masterId, out var master) && _bots.TryGet(master, out var leader)
            && leader.Activity is { IsOver: false, Activity: { FightsItsFights: true } activity } && activity is not FollowActivity
            ? activity.ChallengeReward
            : ChallengeReward.Client;

    /// <summary>Initializes a new instance of the <see cref="FollowActivity"/> class.</summary>
    /// <param name="settings">The activity's settings: the master.</param>
    /// <param name="bots">The host's bots, the master's among them, and their groups.</param>
    /// <param name="tables">The installed build's tables: the items, which say what food heals.</param>
    public FollowActivity(FollowSettings settings, Bots bots, GameTables tables)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bots);
        ArgumentNullException.ThrowIfNull(tables);

        _masterId = settings.Master;
        _bots = bots;
        _tables = tables;
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

    private static ActivityException Refused(FollowRefusal refusal)
    {
        return new ActivityException($"The character cannot follow its master: {refusal}.", GameText.Of(GroupTexts.Text($"Fail{refusal}")));
    }

    // The answer waits, the owner's choice, whatever the client's window for the invitation does.
    private static async Task ShownAsync(BotContext context, CancellationToken cancellationToken)
    {
        var after = _answerAfter > context.Delays.Display ? _answerAfter : context.Delays.Display;

        if (context.Actor.InvitedAt is { } invited && invited + after - context.Time.GetUtcNow() is { } wait && wait > TimeSpan.Zero)
            await Task.Delay(wait, context.Time, cancellationToken).ConfigureAwait(false);
    }

    // Until the master moves, walks, connects, pauses or resumes, the follower's own party or map changes, or the group does.
    private static Task WaitAsync(BotContext context, Task changed, BotSeen mine, BotSeen? leader, CancellationToken cancellationToken)
    {
        var conditions = new List<(BotActor, Func<BotActor, bool>)> { (mine.Bot, mine.Moved) };

        if (leader is not null)
            conditions.Add((leader.Bot, leader.Moved));

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
            await WaitAsync(context, changed, mine, known, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (await JoinFightAsync(context, seen, cancellationToken).ConfigureAwait(false))
            return true;

        var theirs = seen.State;
        var name = GameTextValue.Of(theirs.Name);

        if (GroupChain.Together(theirs, state))
        {
            // Its master taking its objects at the bank (D433, D442).
            if (await GiveAsync(context, seen, cancellationToken).ConfigureAwait(false))
                return true;

            // In real time first (D447): a follower healing would fall a map behind its master, who walks on.
            Publish(GameText.Of(GroupTexts.Text("DoingFollows"), name));

            if (await CopyAsync(context, theirs, cancellationToken).ConfigureAwait(false))
                return true;

            // Its life over the share its master's profile heals under, before its master's next action (D433, D441).
            if (GroupLife.ShareOf(seen.Bot) is { } share && await HealAsync(context, share, changed, mine, seen, cancellationToken).ConfigureAwait(false))
                return true;

            Publish(GameText.Of(GroupTexts.Text("DoingFollows"), name));

            await WaitAsync(context, changed, mine, seen, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // Measured in game (D437): a follower leaving as soon as the server named its master's new map walked to the exit
        // while its client still showed the master there, and its client did not follow that walk. One that copied its
        // master's walk stands on its master's exit and changes maps at once (D447); another leaves once the master is
        // placed on its new map.
        if (theirs.Cell is null && !OnMastersExit(state, theirs))
        {
            Publish(GameText.Of(GroupTexts.Text("DoingFollows"), name));
            await WaitAsync(context, changed, mine, seen, cancellationToken).ConfigureAwait(false);

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
            await WaitAsync(context, changed, mine, seen, cancellationToken).ConfigureAwait(false);

            return true;
        }

        Publish(GameText.Of(GroupTexts.Text("DoingJoins"), name));

        if (!await TowardsAsync(context, map, cancellationToken).ConfigureAwait(false))
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
            await ShownAsync(context, cancellationToken).ConfigureAwait(false);

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
            // Measured (D437): the client turns both on 3 to 22 ms after the party's join, which its display time leaves behind.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
            GroupLog.OptionsOff(context.Log, me.Key.CharacterId, party.Id);
            await context.SendAsync(new PartyAutoOptionOneOffRequest(), cancellationToken).ConfigureAwait(false);
            await context.SendAsync(new PartyAutoOptionTwoOffRequest(), cancellationToken).ConfigureAwait(false);
            _optionsOff = (me.ConnectionId, party.Id);

            return true;
        }

        return false;
    }

    // Whether the follower stands where it copied its master's last walk to, on the map its master just left: its exit.
    private bool OnMastersExit(CharacterState state, CharacterState master)
    {
        return _copiedTo is { } copied && copied.Map == state.MapId && copied.Cell == state.Cell && master.LastWalkEnd == copied.MastersEnd;
    }

    // Whether it copied its master's walk, in real time (D447, the owner's choice): as the master walks, to the cell it walks
    // to, once a walk; a walk it cannot make is left, and the follower joins its master's map by its own paths once the
    // master left it.
    private async Task<bool> CopyAsync(BotContext context, CharacterState master, CancellationToken cancellationToken)
    {
        if (!master.IsWalking || master.Path == _copied)
            return false;

        var state = context.State;
        var end = master.Path[^1];

        _copied = master.Path;

        // The owner's ask (D449): a walk to a group of monsters is its master's attack, whose fight a follower on the map
        // joins from where it stands; walking beside its master first only delayed its join.
        if (master.MonsterGroups.Values.Any(group => group.Cell == end))
            return false;

        // Measured on the owner's retest of 2026-11-19 (D453): a follower beside its master's door needed a walk more onto
        // the door's own cell, which its client did not follow, and the server held it in front of the bank. A walk to a
        // door or a cell that moves the character is its master's way out; the follower takes it once its master took it.
        if (state.MapId is { } here && LeadsAway(context, here, end))
            return false;

        if (state is not { MapId: { } map, Cell: { } from } || Beside(context, master, map, from, end) is not { } cell)
            return false;

        _copiedTo = (map, end, cell);
        GroupLog.Copying(context.Log, context.Actor.Key.CharacterId, master.Key.CharacterId, cell);

        if (await context.WalkAsync(cell, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived)
            _copiedTo = null;

        return true;
    }

    // A free cell beside the one its master walks to, each follower its own, by its place in its master's party, that it can
    // reach; none when it stands beside it already. Measured on the owner's test of 2026-11-19 (D448): the master and its
    // three followers walking to one cell at once, none of their clients confirmed its walk, and the server held each
    // follower there. The owner's choice: beside the master, by its own path.
    private int? Beside(BotContext context, CharacterState master, long map, int from, int end)
    {
        if (!context.Maps.TryGet(map, out var data))
            return null;

        var followers = GroupChain.Order(master, _bots.Groups.FollowersOf(master.Key));
        var at = followers.Select((bot, index) => (bot, index)).FirstOrDefault(pair => pair.bot == context.Actor).index;

        // Measured on the owner's retest of 2026-11-19 (D449): around a cell on a map's edge, one neighbour alone was free,
        // and three followers walked to it at once, two of their walks unconfirmed. The cells around, then those around
        // them, none another member stands on or walks to; each follower its own among them, by its place in the party, so
        // that followers choosing at once choose apart.
        var taken = followers.Where(bot => bot != context.Actor)
            .SelectMany(bot => new[] { bot.State.Cell, bot.State.IsWalking ? bot.State.Path[^1] : (int?)null })
            .Where(cell => cell is not null)
            .Select(cell => cell!.Value)
            .Concat(context.TakenCells())
            .Append(end)
            .Append(master.Cell ?? -1)
            .ToHashSet();
        var near = Around(end).ToList();
        var rings = near.Concat(near.SelectMany(Around)).Distinct().Where(cell => !taken.Contains(cell)).ToList();
        var cells = data.Cells;

        // Measured on the owner's retest of 2026-11-19 (D451): a follower stopped beside its master's exit needed one step
        // more onto an exit, which its client did not follow, and the server held it, its map changes unanswered, until its
        // journey went round. To its master's exit, it walks to an exit of its own, which leaves the map the same way.
        if (cells.Span[end].MapChangeData is not 0 and var leaves)
        {
            var exits = rings.Where(cell => (cells.Span[cell].MapChangeData & leaves) is not 0).ToList();

            if (exits.Contains(from))
                return null;

            if (exits.Count > 0)
                rings = exits;
        }
        else if (near.Contains(from) && !taken.Contains(from))
        {
            return null;
        }

        var free = rings.Where(cell => cell == from || CellPathfinder.TryFind(data.Cells.Span, from, cell, out _)).ToList();

        return free.Count is 0 ? null : free[Math.Min(at, free.Count - 1)] is var cell && cell != from ? cell : null;
    }

    // Whether a cell of the map is where a door is used or a cell moves the character to another map, by the world's graph.
    private static bool LeadsAway(BotContext context, long map, int cell)
    {
        if (!context.Maps.TryGet(map, out var data) || !context.World.TryGetVertex(data, cell, out var vertex))
            return false;

        var graph = context.World.Graph;

        foreach (var edge in graph.EdgesFrom(vertex))
        {
            foreach (var transition in graph.TransitionsOf(edge))
            {
                if (transition.CellId == cell && transition.Type is WorldTransitionType.Interactive or WorldTransitionType.MapAction)
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<int> Around(int cell)
    {
        return Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(cell, direction)).Where(neighbour => neighbour >= 0);
    }

    // Whether it asked to join its master's fight: as the master attacks on its map, the server tells the party of the fight
    // in the millisecond it shows it there (D435), and the follower joins it by its id and the master's, as the owner's
    // click did. The end of a member's fight no message measured says: the master's own state, in the same host, says
    // whether its fight is still in its placement, which a follower may join. Once a fight and a connection, so that a join
    // the server answers nothing to is not sent again and again. The party's word stays until the follower's context
    // changes: one seen while its master fights no more is over, never joined, lest the next fight on the map join it.
    private async Task<bool> JoinFightAsync(BotContext context, BotSeen master, CancellationToken cancellationToken)
    {
        var state = context.State;
        var me = context.Actor;

        if (state.PartyFight is { } told && master.State.Fight is null)
            _over = told.FightId;

        if (state.IsFighting || state.PartyFight is not { } fight || fight.MemberId != master.Bot.Key.CharacterId || fight.MapId != state.MapId
            || master.State.Fight is not { Phase: FightPhase.Placement } || _joined == (me.ConnectionId, fight.FightId) || _over == fight.FightId)
        {
            return false;
        }

        // Measured (D448): a follower whose walk its client did not confirm is held by the server, which answered nothing to
        // its join; a step aside lets it go (D85), then it joins.
        if (state.IsWalkHeld)
        {
            await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }

        _joined = (me.ConnectionId, fight.FightId);
        Publish(GameText.Of(GroupTexts.Text("DoingJoinsFight"), GameTextValue.Of(master.State.Name)));
        GroupLog.JoiningFight(context.Log, me.Key.CharacterId, fight.MemberId, fight.FightId);
        await context.SendAsync(new FightJoinRequest { FightId = fight.FightId, FighterId = fight.MemberId }, cancellationToken).ConfigureAwait(false);
        await context.WaitUntilAsync(now => now.IsFighting, context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        return true;
    }

    // Whether it healed this step: once under its master's share, it eats until its life is whole, the food it carries that
    // only heals, smallest first, as a profile choosing none does (D148), the owner's choice; with none, its life regenerates
    // up to the share (9g5). The master's gate waits for it meanwhile (D433).
    private async Task<bool> HealAsync(BotContext context, int share, Task changed, BotSeen mine, BotSeen master, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Life is not { } life || state.MaxLife is not { } most || most <= 0)
            return false;

        if (life >= most || (!_healing && life >= GroupLife.PointsOf(share, most)))
        {
            _healing = false;

            return false;
        }

        _healing = true;

        if (Food.StackToEat(state, [], _tables.Items, _unusable) is { } food)
        {
            Publish(GameText.Of(GroupTexts.Text("DoingEat"), GameTextValue.Item(food.ItemId)));

            // In one request: measured, the server eats only as much of the stack as the life lacks (D156).
            switch (await context.UseObjectsAsync(food.Uid, food.Quantity, cancellationToken).ConfigureAwait(false))
            {
                case ObjectUseOutcome.Used:
                    break;
                case ObjectUseOutcome.Refused:
                    // Measured: the server refuses food at full life, which it knows better than the reckoning.
                    _healing = false;
                    break;
                default:
                    _unusable.Add(food.Uid);
                    break;
            }

            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (GroupLife.Lacking(context.Actor, share, context.Time.GetUtcNow()) is var lacking && lacking <= TimeSpan.Zero)
        {
            _healing = false;

            return false;
        }

        Publish(GameText.Of(GroupTexts.Text("DoingRegenerate"), GameTextValue.Number((long)Math.Ceiling(lacking.TotalSeconds))));
        // Its master moving, or a fight told on its map, ends the wait as the follower's own waits do.
        await GroupWaits.AnyAsync([(mine.Bot, mine.Moved), (master.Bot, master.Moved)], changed, context.Time, lacking < _lookAgain ? lacking : _lookAgain, cancellationToken).ConfigureAwait(false);

        return true;
    }

    // Whether it gave this step: its master taking its objects at the bank (D433, D442), it asks the master for an exchange
    // once a round, moves in, one by one, what the master's pods still hold of the stacks it does not wear, never its kamas,
    // then says it is ready naming how many changes the server told it of, as the client did (D442), no sooner than the
    // server takes it (D458). Its master accepts and concludes.
    private async Task<bool> GiveAsync(BotContext context, BotSeen master, CancellationToken cancellationToken)
    {
        var me = context.Actor;
        var masterId = master.Bot.Key.CharacterId;

        if (_bots.Groups.CollectingFrom(master.Bot.Key) is not { } collect || collect.Follower != me.Key)
            return false;

        var state = context.State;

        if (state.Exchange is not { IsOpen: true } exchange)
        {
            if (_giving == (me.ConnectionId, collect.Round))
                return false;

            _giving = (me.ConnectionId, collect.Round);
            Publish(GameText.Of(GroupTexts.Text("DoingGives"), GameTextValue.Of(master.State.Name)));
            GroupLog.AskingExchange(context.Log, me.Key.CharacterId, masterId);
            await context.SendAsync(new ExchangePlayerRequest { TargetId = masterId }, cancellationToken).ConfigureAwait(false);
            await context.WaitUntilAsync(now => now.Exchange is { IsOpen: true }, _exchangeWait, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (exchange.IsReady || exchange.SourceId != me.Key.CharacterId)
            return false;

        foreach (var (uid, quantity) in GroupGifts.Of(state, master.Bot.State, _tables.Items))
        {
            // Its master gave up the exchange, or the player closed it: nothing more goes in.
            if (context.State.Exchange is not { IsOpen: true })
                return true;

            await MovedAsync(context, new ExchangeObjectMoveRequest { Uid = uid, Quantity = quantity }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
        }

        if (context.State.Exchange is not { IsOpen: true })
            return true;

        // Until 3.5 s passed with no change told, whichever moves them, as the server takes no readiness sooner (D458).
        for (var seen = context.State.Exchange.Changes; ; seen = context.State.Exchange?.Changes ?? seen)
        {
            if (!await context.WaitUntilAsync(now => now.Exchange is not { IsOpen: true } open || open.Changes != seen, _readyAfterChange, cancellationToken).ConfigureAwait(false))
                break;

            if (context.State.Exchange is not { IsOpen: true })
                return true;
        }

        // Its readiness unanswered, it says it again, as long as the exchange is open: the master waits 30 s for it.
        for (var attempt = 0; attempt < 3 && context.State.Exchange is { IsOpen: true, IsReady: false } moved; attempt++)
        {
            await context.SendAsync(new ExchangeReadyRequest { Step = moved.Changes, Ready = true }, cancellationToken).ConfigureAwait(false);
            await context.WaitUntilAsync(now => now.Exchange is not { IsOpen: true, IsReady: false }, _readyAgainAfter, cancellationToken).ConfigureAwait(false);
        }

        await context.WaitUntilAsync(now => now.Exchange is null, _exchangeWait, cancellationToken).ConfigureAwait(false);

        return true;
    }

    // A move into the exchange, until the server told its change, measured 28 ms after (D442), or did not, such as for an
    // object bound to the account (D153), which changes nothing and the next goes on.
    private static async Task MovedAsync(BotContext context, ExchangeObjectMoveRequest move, CancellationToken cancellationToken)
    {
        var changes = context.State.Exchange?.Changes ?? 0;

        await context.SendAsync(move, cancellationToken).ConfigureAwait(false);
        await context.WaitUntilAsync(now => now.Exchange is not { IsOpen: true } open || open.Changes > changes, context.Delays.Answer, cancellationToken).ConfigureAwait(false);
    }

    // One change towards the master's map a step, so that a change failing tries the same way again at the next step: a
    // journey would leave that way out and go round (D74), as measured in game (D437) after a walk the client did not
    // follow. Only after several in a row does a whole journey find another way, and never while the master is one change
    // away: measured on the owner's runs of 2026-11-19 (D456), such a journey went round by nine maps, the way the master
    // had just taken failing only for the follower the server held.
    private async Task<bool> TowardsAsync(BotContext context, long map, CancellationToken cancellationToken)
    {
        var maps = FrozenSet.Create(map);
        var state = context.State;
        var rules = WorldTravel.RulesOf(context);

        if (state is not { MapId: { } here, Cell: { } cell } || !context.Maps.TryGet(here, out var data) || !context.World.TryGetVertex(data, cell, out var vertex)
            || !context.World.TryFind(vertex, maps, rules, out var path) || path.Steps.Count is 0)
        {
            return await context.TravelAsync(maps, cancellationToken).ConfigureAwait(false) is TravelOutcome.Arrived;
        }

        if (_failedChanges >= MostFailedChanges && path.Steps.Count > 1)
        {
            _failedChanges = 0;

            return await context.TravelAsync(maps, cancellationToken).ConfigureAwait(false) is TravelOutcome.Arrived;
        }

        var changed = await context.ChangeMapAsync(path.Steps[0], rules, cancellationToken).ConfigureAwait(false) is MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere;

        _failedChanges = changed ? 0 : _failedChanges + 1;

        return changed;
    }

    // Stuck: the group waits for it (D432), and it tries again once its master moved, or after a while.
    private Task StuckAsync(BotContext context, BotSeen master, GameTextValue name, CancellationToken cancellationToken)
    {
        Publish(GameText.Of(GroupTexts.Text("DoingStuck"), name));

        return WaitAsync(context, _bots.Groups.Changed, BotSeen.Of(context.Actor), master, cancellationToken);
    }

    private void Publish(GameText doing)
    {
        _report = new ActivityReport { Doing = doing };
    }
}
