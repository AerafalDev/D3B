using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>A bot's part in its group's fights: the master's fight left to its party, and its wait for its followers' placement (D431, D434).</summary>
/// <remarks>
/// <para>
/// Measured on 3.7.14.18 (D435): the server locks a party's leader's fight to its party by itself as it attacks, and the
/// master's closing of it, which the AI did alone (D216), would keep its followers out. So a master refuses spectators
/// only, and a follower, whose team's options are its master's, sets none.
/// </para>
/// <para>
/// A master says it is ready once each follower that joins its fight is placed and ready (D434): a follower in the
/// placement not yet ready, or on the fight's map not yet in it, is waited for; one disconnected, paused or elsewhere is
/// not, nor one in another fight. The fight starting ends the wait, as the delay the AI gives does.
/// </para>
/// </remarks>
internal sealed class GroupFight : IFightTeam
{
    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;

    /// <inheritdoc/>
    public FightRole Role => _groups.MasterOf(_bot.Key) is not null ? FightRole.Follows
        : _groups.FollowersOf(_bot.Key).Count > 0 ? FightRole.Leads
        : FightRole.Alone;

    public GroupFight(BotActor bot, GroupDirectory groups)
    {
        _bot = bot;
        _groups = groups;
    }

    /// <summary>Tells whether a follower is still to place in its master's fight on a map.</summary>
    /// <param name="follower">The follower's bot.</param>
    /// <param name="mapId">The map the master fights on.</param>
    /// <returns>Whether the master waits for it.</returns>
    public static bool Placing(BotActor follower, long mapId)
    {
        ArgumentNullException.ThrowIfNull(follower);

        var state = follower.State;

        if (!follower.IsConnected || follower.IsPaused || state.MapId != mapId)
            return false;

        return state.Fight is null ? state.Cell is not null : state.Fight is { Phase: FightPhase.Placement, IsReady: false };
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

            var placing = followers.Where(follower => Placing(follower, mapId)).Select(follower => follower.Key.CharacterId).ToList();

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
