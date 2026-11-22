using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Harvesting;

/// <summary>Harvesting the map the character stands on: which element to take next, and a harvest of the whole map (D127, D384).</summary>
/// <remarks>
/// The activity chooses its next element with this and goes on through its zone; a route's action (D384) harvests what the
/// map offers of its list and goes on to the route's next step, the route and not the zone deciding where.
/// </remarks>
public static class MapHarvest
{
    // An element the server keeps answering "busy" to is left, so that the harvest does not spin on it.
    private const int MostBusy = 3;

    // Measured (D75): the objects come within milliseconds of the harvest's end, in no set order.
    private static readonly TimeSpan _objectsDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);

    /// <summary>Gets a character's level in a profession.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="jobId">The profession.</param>
    /// <returns>The level; one the server has not described yet counts as the first, every character having every profession.</returns>
    public static int LevelOf(CharacterState state, int jobId)
    {
        return state.Jobs.TryGetValue(jobId, out var job) && job.Level > 0 ? job.Level : 1;
    }

    /// <summary>Counts what a character carries of an item, worn items left out.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>How many.</returns>
    public static int Carried(CharacterState state, int itemId)
    {
        return state.Inventory.Values.Where(stack => stack.ItemId == itemId && !stack.IsWorn).Sum(stack => stack.Quantity);
    }

    /// <summary>Lists the elements of the map the character can harvest, the first to take first.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="state">The character's state.</param>
    /// <param name="skipped">The elements left for the visit: taken by another, refused or out of reach; the list adds those it cannot reach.</param>
    /// <returns>The elements with the target each is harvested for: the resource asked for first, then the nearest.</returns>
    public static List<(int ElementId, HarvestTarget Target)> Candidates(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CharacterState state, HashSet<int> skipped)
    {
        var found = new List<(int ElementId, HarvestTarget Target, int Steps)>();

        if (state.Cell is not { } cell || !context.Maps.TryGet(state.MapId!.Value, out var map))
            return [];

        var cells = map.Cells.Span;

        foreach (var element in state.Elements.Values)
        {
            if (skipped.Contains(element.ElementId))
                continue;

            if (BestTarget(state, targets, element) is not { } chosen)
                continue;

            if (!ElementUse.TryGetCell(map, element.ElementId, out var elementCell)
                || !RoleplayPaths.TryFindNearest(context, map, cell, [.. ElementCells.UsedFrom(cells, elementCell, cells[cell].Zone)], out var path))
            {
                skipped.Add(element.ElementId);
                BotLog.OutOfReach(context.Log, state.Key.CharacterId, element.ElementId, cells[cell].Zone);

                continue;
            }

            found.Add((element.ElementId, chosen, path.Steps));
        }

        return [.. found.OrderBy(candidate => candidate.Target.Priority).ThenBy(candidate => candidate.Steps).ThenBy(candidate => candidate.ElementId).Select(candidate => (candidate.ElementId, candidate.Target))];
    }

    /// <summary>Tells whether a wanted element the character can reach is offered on its map that was not as it came onto it.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="came">The character's state as it came onto the map, or as a journey started there.</param>
    /// <param name="now">The character's state now, on that map.</param>
    /// <returns>Whether such an element grew back, or was left by whoever was harvesting it, since: what a journey stays on its map for (D495).</returns>
    /// <remarks>
    /// Measured on the owner's captures of 18 to 22 November: 136 resources grew back as the walk to the map's edge went, the character
    /// leaving them behind. An element offered already as the character came is the tour's to weigh, not a reason to stay.
    /// </remarks>
    public static bool Regrown(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CharacterState came, CharacterState now)
    {
        ArgumentNullException.ThrowIfNull(came);
        ArgumentNullException.ThrowIfNull(now);

        if (now.MapId is null || came.MapId != now.MapId)
            return false;

        var offered = came.Elements.Values.Where(element => BestTarget(came, targets, element) is not null).Select(element => element.ElementId).ToHashSet();

        return Candidates(context, targets, now, offered).Count > 0;
    }

    /// <summary>Lists the zones of the map, other than the character's, where the wanted resources it cannot use from its own stand.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="state">The character's state.</param>
    /// <returns>The zones, in ascending order, one for each such element: its lowest zone, unless a zone already chosen for another one borders it; an element with a walkable neighbour in the character's zone is harvested from there, one with no walkable neighbour (water) names none.</returns>
    /// <remarks>What a tour that left such elements behind goes to another zone of the map for (D425).</remarks>
    public static IReadOnlyList<int> OtherZones(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CharacterState state)
    {
        if (state.Cell is not { } cell || !context.Maps.TryGet(state.MapId!.Value, out var map))
            return [];

        var cells = map.Cells.Span;
        var zone = cells[cell].Zone;
        var left = new List<IReadOnlyList<int>>();

        foreach (var element in state.Elements.Values)
        {
            if (BestTarget(state, targets, element) is null || !ElementUse.TryGetCell(map, element.ElementId, out var elementCell) || ElementCells.UsedFrom(cells, elementCell, zone).Count > 0)
                continue;

            var zones = ElementCells.NeighbourZones(cells, elementCell);

            if (zones.Count > 0)
                left.Add(zones);
        }

        // An element with fewest zones to choose from first; one a zone already chosen borders adds none. The character has none of these
        // zones as its own, so each element lists only other zones.
        var chosen = new SortedSet<int>();

        foreach (var zones in left.OrderBy(zones => zones.Count))
        {
            if (!zones.Any(chosen.Contains))
                chosen.Add(zones[0]);
        }

        return [.. chosen];
    }

    /// <summary>Finds the part of the map, in the character's zone, where wanted elements stand that it cannot walk to, and the side to come onto it from.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="state">The character's state.</param>
    /// <returns>The part, entered by a scroll from a neighbouring map of the world graph, the scroll leaving it from a cell that leads to the map; none when every wanted element is reached, stands in another zone (<see cref="OtherZones"/>), or no side leads to those cut off.</returns>
    /// <remarks>
    /// Measured on the owner's miner (D495): the ore below a cliff, its neighbours on its floor cut from the character's floor, the
    /// server refusing its use from above; the only way to them was to come onto the map from its west, a map away.
    /// </remarks>
    internal static MapLevel? OtherLevel(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CharacterState state)
    {
        if (state.Cell is not { } cell || !context.Maps.TryGet(state.MapId!.Value, out var map))
            return null;

        var cells = map.Cells.Span;
        var zone = cells[cell].Zone;
        var cut = new List<int>();

        foreach (var element in state.Elements.Values)
        {
            if (BestTarget(state, targets, element) is null || !ElementUse.TryGetCell(map, element.ElementId, out var elementCell))
                continue;

            var used = ElementCells.UsedFrom(cells, elementCell, zone);

            if (used.Count > 0 && !RoleplayPaths.TryFindNearest(context, map, cell, [.. used], out _))
                cut.AddRange(used);
        }

        if (cut.Count is 0)
            return null;

        foreach (var (side, edge) in MapEntries.Reaching(cells, cut, zone))
        {
            var neighbourId = side switch { 0 => map.RightNeighbourId, 2 => map.BottomNeighbourId, 4 => map.LeftNeighbourId, _ => map.TopNeighbourId };

            if (neighbourId is 0 || !context.World.Graph.Contains(neighbourId) || !context.Maps.TryGet(neighbourId, out var neighbour))
                continue;

            var across = neighbour.Cells.Span;
            var towards = (side + (MapCell.DirectionCount / 2)) % MapCell.DirectionCount;
            var arrivals = ImmutableArray.CreateBuilder<int>();

            foreach (var arrival in edge)
            {
                var left = MapEntries.LeftFrom(arrival, side);

                if (left is >= 0 and < MapData.CellCount && across[left].IsWalkable && MapGeometry.LeavesTowards(across[left], left, towards))
                    arrivals.Add(arrival);
            }

            if (arrivals.Count > 0)
                return new MapLevel(map.Id, side, neighbourId, arrivals.ToImmutable());
        }

        return null;
    }

    /// <summary>Harvests every element of the map that the targets name, one after the other, until none is left.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="cancellationToken">The behavior's token: a fight that begins cuts it, and the route plays the action again once it is over.</param>
    /// <returns>How many elements it harvested.</returns>
    public static async Task<int> HarvestAsync(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CancellationToken cancellationToken)
    {
        var skipped = new HashSet<int>();
        var harvested = 0;
        var busy = 0;

        while (true)
        {
            var state = context.State;

            if (state.IsBusy || state.MapId is null)
            {
                await context.WaitUntilAsync(waited => !waited.IsBusy && waited.MapId is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

                if (context.State.IsBusy || context.State.MapId is null)
                    return harvested;

                continue;
            }

            var candidates = Candidates(context, targets, state, skipped);

            if (candidates.Count is 0)
                return harvested;

            var (elementId, target) = candidates[0];
            var before = Carried(state, target.ItemId);

            switch (await context.UseAsync(elementId, target.SkillId, cancellationToken).ConfigureAwait(false))
            {
                case UseOutcome.Used:
                    await context.WaitUntilAsync(after => Carried(after, target.ItemId) != before, _objectsDelay, cancellationToken).ConfigureAwait(false);
                    harvested++;
                    break;
                case UseOutcome.Busy when ++busy <= MostBusy:
                    break;
                default:
                    skipped.Add(elementId);
                    busy = 0;
                    break;
            }
        }
    }

    // The target an element is harvested for, if it is offered one the character can harvest: the lowest priority wins.
    private static HarvestTarget? BestTarget(CharacterState state, FrozenDictionary<int, HarvestTarget> targets, MapElement element)
    {
        HarvestTarget? best = null;

        foreach (var skill in element.EnabledSkills)
        {
            if (targets.TryGetValue(skill.SkillId, out var target) && LevelOf(state, target.JobId) >= target.Level
                && (best is null || target.Priority < best.Value.Priority))
            {
                best = target;
            }
        }

        return best;
    }
}
