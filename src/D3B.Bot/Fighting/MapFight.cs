using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Fighting;

/// <summary>Fighting the groups of the map the character stands on that a filter lets through (D384).</summary>
/// <remarks>
/// <para>
/// A route's action: the owner named the groups, by their size and the monsters they hold, so no judgement is passed on them,
/// neither by the character's level nor by the AI's odds that the activity weighs (D236, D249). The nearest group is attacked
/// first; one that cannot be reached, or whose attack the server answers with no fight, is left for the visit.
/// </para>
/// <para>
/// A fight that begins interrupts the step the action is played in; the route plays the action again once the fight is over
/// and the bot resumed, and the groups left are fought. The action is over when no group the filter lets through is left.
/// </para>
/// </remarks>
public static class MapFight
{
    // Measured (9g): a group walks 1 to 4 cells at a time; the server answered nothing to an attack from 3 cells away.
    private const int MostWalksToAGroup = 3;

    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);

    /// <summary>Tells whether a filter lets a group through.</summary>
    /// <param name="group">The group.</param>
    /// <param name="monsters">The monsters a group must hold one of, by id; none, every group.</param>
    /// <param name="least">The fewest monsters a group holds.</param>
    /// <param name="most">The most monsters a group holds.</param>
    /// <returns>Whether it does; a group with no cell is never let through, there being nowhere to walk to.</returns>
    public static bool Allows(MapMonsterGroup group, IReadOnlyList<int> monsters, int least, int most)
    {
        var size = group.Monsters.Length;

        if (size < least || size > most || group.Cell is null)
            return false;

        return monsters.Count is 0 || group.Monsters.Any(monster => monsters.Contains(monster.MonsterId));
    }

    /// <summary>Fights the groups of the map the filter lets through, one after the other, until none is left.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="monsters">The monsters a group must hold one of, by id; none, every group.</param>
    /// <param name="least">The fewest monsters a group holds.</param>
    /// <param name="most">The most monsters a group holds.</param>
    /// <param name="cancellationToken">The behavior's token: a fight that begins cuts it.</param>
    /// <returns>How many groups it attacked.</returns>
    public static async Task<int> FightAsync(BotContext context, IReadOnlyList<int> monsters, int least, int most, CancellationToken cancellationToken)
    {
        var skipped = new HashSet<long>();
        var attacked = 0;

        while (true)
        {
            var state = context.State;

            if (state.IsBusy || state.MapId is null)
            {
                await context.WaitUntilAsync(waited => !waited.IsBusy && waited.MapId is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

                if (context.State.IsBusy || context.State.MapId is null)
                    return attacked;

                continue;
            }

            if (GroupTo(state, monsters, least, most, skipped) is not { } group)
                return attacked;

            attacked++;

            if (!await AttackAsync(context, group, cancellationToken).ConfigureAwait(false))
                skipped.Add(group.ActorId);
        }
    }

    private static MapMonsterGroup? GroupTo(CharacterState state, IReadOnlyList<int> monsters, int least, int most, HashSet<long> skipped)
    {
        if (state.Cell is not { } here)
            return null;

        return state.MonsterGroups.Values
            .Where(group => !skipped.Contains(group.ActorId) && Allows(group, monsters, least, most))
            .OrderBy(group => MapGeometry.Distance(here, group.Cell!.Value))
            .ThenBy(group => group.ActorId)
            .Cast<MapMonsterGroup?>()
            .FirstOrDefault();
    }

    private static async Task<bool> AttackAsync(BotContext context, MapMonsterGroup group, CancellationToken cancellationToken)
    {
        for (var walk = 0; walk < MostWalksToAGroup; walk++)
        {
            if (!context.State.MonsterGroups.TryGetValue(group.ActorId, out var now) || now.Cell is not { } cell)
                return false;

            if (context.State.Cell != cell && await context.WalkAsync(cell, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived)
                break;

            if (!context.State.MonsterGroups.TryGetValue(group.ActorId, out now) || now.Cell != context.State.Cell)
                continue;

            var fought = context.Expect((side, _, state) => state.Fight is not null);

            await context.SendAsync(new AttackMonsterRequest { MonsterGroupId = group.ActorId }, cancellationToken).ConfigureAwait(false);

            // A refusal is no answer at all, measured (9g).
            return await fought.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is not null;
        }

        return false;
    }
}
