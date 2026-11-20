using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>A bot's part in its group's fights: the master's fight left to its party, and its wait for its followers' placement (D431, D434).</summary>
/// <remarks>
/// <para>
/// Measured on 3.7.14.18 (D435, D471): the server locks a member's fight to its party by itself as it attacks, its leader's or
/// another's, and the master's closing of it, which the AI did alone (D216), would keep its followers out. So a master with
/// followers, or whose fight the server locked to its party, refuses spectators only and opens a fight the server kept
/// closed from its last (D471), and a follower in its master's fight, whose team's options are its master's, sets none. Any
/// other fight, a follower's own such as an aggression on its way, or one of a character out of a party nobody follows, is
/// the bot's own, which it closes as one alone does.
/// </para>
/// <para>
/// A master says it is ready once each follower that joins its fight is placed and ready (D434), or placed when its readiness is the game's (D477): a follower in the master's
/// fight not yet ready, or on the fight's map in the master's party and not yet in a fight, is waited for; one
/// disconnected, paused, elsewhere, out of the party or in another fight is not. The fight starting ends the wait, as the
/// delay the AI gives does.
/// </para>
/// <para>
/// In a group the game follows (D476, D477), the server sets each follower ready as its master is, measured on the owner's games of
/// 2026-11-20: a follower places itself and says nothing more, and its master waits for it placed, not ready.
/// </para>
/// </remarks>
internal sealed class GroupFight : IFightTeam
{
    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;
    private readonly Bots _bots;
    private volatile bool _placed;
    private volatile bool _joinedByRequest;

    /// <inheritdoc/>
    public FightRole Role
    {
        get
        {
            var fight = _bot.State.Fight;

            if (_groups.MasterOf(_bot.Key) is { } master)
                return fight?.Fighters.ContainsKey(master.CharacterId) == true ? FightRole.Follows : FightRole.Alone;

            // The owner's report of 2026-11-20 (D471): out of its party for two minutes, the master fought closed, and the server
            // kept its next fight closed, its followers' joins refused. A master its followers are known to closes none.
            return _groups.FollowersOf(_bot.Key).Count > 0 || fight?.Options.GetValueOrDefault(FightOption.PartyOnly) == true ? FightRole.Leads : FightRole.Alone;
        }
    }

    /// <inheritdoc/>
    public bool LeavesReadyToTheGame
        => !_joinedByRequest && Role is FightRole.Follows && _groups.MasterOf(_bot.Key) is { } master && _bots.TryGet(master, out var leader)
            && GameFollowing.Applies(leader.State, [.. _groups.FollowersOf(master).Select(follower => follower.State)], _bot.Time.GetUtcNow());

    /// <inheritdoc/>
    public bool IsPlaced => _placed;

    public GroupFight(BotActor bot, GroupDirectory groups, Bots bots)
    {
        _bot = bot;
        _groups = groups;
        _bots = bots;
    }

    /// <summary>Tells whether a follower is still to place in its master's fight, begun from a map.</summary>
    /// <param name="follower">The follower's bot.</param>
    /// <param name="masterId">The master's character.</param>
    /// <param name="mapId">The map the master's fight began from, out of fights (D448).</param>
    /// <returns>Whether the master waits for it.</returns>
    public static bool Placing(BotActor follower, long masterId, long mapId)
    {
        ArgumentNullException.ThrowIfNull(follower);

        var state = follower.State;

        if (!follower.IsConnected || follower.IsPaused)
            return false;

        // Out of a fight, only a member of the master's party on the map the fight began from is told of it, and joins it (D435).
        if (state.Fight is not { } fight)
            return state.MapId == mapId && state.Cell is not null && state.Party?.Members.Contains(masterId) == true;

        if ((fight.FromMapId ?? state.MapId) != mapId)
            return false;

        // A placement whose cells are not yet told may be the master's still being drawn, its fighters shown one by one; once
        // told, one the master is not in is another fight.
        // A follower that leaves its readiness to the game is waited for until placed (D477).
        return fight is { Phase: FightPhase.Placement, IsReady: false } && (fight.Fighters.ContainsKey(masterId) || fight.OwnCells.IsEmpty)
            && follower.Team?.IsPlaced != true;
    }

    /// <inheritdoc/>
    public async Task PlacedAsync(BotContext context, TimeSpan atMost, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var until = _bot.Time.GetUtcNow() + atMost;
        var said = false;

        while (true)
        {
            // Seen before anything is decided from them, so that what changes after wakes the wait below.
            var changed = _groups.Changed;
            var state = _bot.State;
            var followers = _groups.FollowersOf(_bot.Key);
            var seen = followers.Select(BotSeen.Of).ToList();
            var left = until - _bot.Time.GetUtcNow();

            // Measured (D448): the fight's own map is not the one it began from, where the followers stand.
            if (state is not { Fight: { Phase: FightPhase.Placement } fight } || (fight.FromMapId ?? state.MapId) is not { } mapId || left <= TimeSpan.Zero)
                return;

            var placing = followers.Where(follower => Placing(follower, _bot.Key.CharacterId, mapId)).Select(follower => follower.Key.CharacterId).ToList();

            if (placing.Count is 0)
            {
                if (said)
                    GroupLog.FollowersPlaced(_bot.Log, _bot.Key.CharacterId);

                return;
            }

            if (!said)
            {
                said = true;

                if (_bot.Log.IsEnabled(LogLevel.Information))
                {
                    var names = string.Join(", ", placing);

                    GroupLog.WaitingPlacement(_bot.Log, _bot.Key.CharacterId, names);
                }
            }

            (BotActor, Func<BotActor, bool>) own = (_bot, bot => bot.State.Fight is not { Phase: FightPhase.Placement });

            await GroupWaits.AnyAsync([.. seen.Select(one => (one.Bot, (Func<BotActor, bool>)one.Changed)), own], changed, _bot.Time, left, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Placed(bool placed)
    {
        if (_placed == placed)
            return;

        _placed = placed;

        // No state of the follower's shows it: its master's wait for it would see nothing change.
        _groups.Touch();
    }

    /// <inheritdoc/>
    /// <remarks>The review of D477: the server joins only a follower whose options are on, and sets ready only such a one.</remarks>
    public void JoinedByRequest()
    {
        _joinedByRequest = true;
    }

    /// <inheritdoc/>
    public void FightOver()
    {
        _joinedByRequest = false;
        Placed(false);
    }
}
