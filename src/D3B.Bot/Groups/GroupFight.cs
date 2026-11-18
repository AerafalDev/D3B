using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>A bot's part in its group's fights: the master's fight left to its party, and its wait for its followers' placement (D431, D434).</summary>
/// <remarks>
/// <para>
/// Measured on 3.7.14.18 (D435): the server locks a party's leader's fight to its party by itself as it attacks, and the
/// master's closing of it, which the AI did alone (D216), would keep its followers out. So a master whose fight the server
/// locked to its party refuses spectators only, and a follower in its master's fight, whose team's options are its
/// master's, sets none. Any other fight of theirs, such as an aggression on the way or a fight of a master out of a party,
/// is the bot's own, which it closes as one alone does.
/// </para>
/// <para>
/// A master says it is ready once each follower that joins its fight is placed and ready (D434): a follower in the master's
/// fight not yet ready, or on the fight's map in the master's party and not yet in a fight, is waited for; one
/// disconnected, paused, elsewhere, out of the party or in another fight is not. The fight starting ends the wait, as the
/// delay the AI gives does.
/// </para>
/// </remarks>
internal sealed class GroupFight : IFightTeam
{
    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;

    /// <inheritdoc/>
    public FightRole Role
    {
        get
        {
            var fight = _bot.State.Fight;

            if (_groups.MasterOf(_bot.Key) is { } master)
                return fight?.Fighters.ContainsKey(master.CharacterId) == true ? FightRole.Follows : FightRole.Alone;

            return _groups.FollowersOf(_bot.Key).Count > 0 && fight?.Options.GetValueOrDefault(FightOption.PartyOnly) == true ? FightRole.Leads : FightRole.Alone;
        }
    }

    public GroupFight(BotActor bot, GroupDirectory groups)
    {
        _bot = bot;
        _groups = groups;
    }

    /// <summary>Tells whether a follower is still to place in its master's fight on a map.</summary>
    /// <param name="follower">The follower's bot.</param>
    /// <param name="masterId">The master's character.</param>
    /// <param name="mapId">The map the master fights on.</param>
    /// <returns>Whether the master waits for it.</returns>
    public static bool Placing(BotActor follower, long masterId, long mapId)
    {
        ArgumentNullException.ThrowIfNull(follower);

        var state = follower.State;

        if (!follower.IsConnected || follower.IsPaused || state.MapId != mapId)
            return false;

        // Out of a fight, only a member of the master's party is told of its fight, and joins it (D435).
        if (state.Fight is not { } fight)
            return state.Cell is not null && state.Party?.LeaderId == masterId;

        // A placement whose cells are not yet told may be the master's still being drawn, its fighters shown one by one; once
        // told, one the master is not in is another fight.
        return fight is { Phase: FightPhase.Placement, IsReady: false } && (fight.Fighters.ContainsKey(masterId) || fight.OwnCells.IsEmpty);
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

            if (state is not { Fight.Phase: FightPhase.Placement, MapId: { } mapId } || left <= TimeSpan.Zero)
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
}
