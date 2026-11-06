using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Engine.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Movement;

/// <summary>Map changes the owner asks for by hand, as a route's actions do: a side or a cell to leave by, a door's cell, the haven bag (D384).</summary>
/// <remarks>
/// <para>
/// None goes through the world's graph: the character leaves by what the owner named, whether or not a path of the graph
/// would. They send what <see cref="MapChange"/> sends, a scroll's request for the neighbour, a door's use, the haven bag's
/// entry, and are over the same way, once the character is placed on the new map.
/// </para>
/// <para>
/// The haven bag is entered by the request that leaves it, naming the character's own bag (D83): the server takes the
/// character to the bag from a map, and back from the bag.
/// </para>
/// </remarks>
public static class ForcedMoves
{
    /// <summary>The straight direction of the map's top, towards the map north of it.</summary>
    public const int North = 6;

    /// <summary>The straight direction of the map's right, towards the map east of it.</summary>
    public const int East = 0;

    /// <summary>The straight direction of the map's bottom, towards the map south of it.</summary>
    public const int South = 2;

    /// <summary>The straight direction of the map's left, towards the map west of it.</summary>
    public const int West = 4;

    /// <summary>Leaves the map by a side, or by a cell of its edge, to the neighbour there, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="direction">The side to leave by, <see cref="North"/>, <see cref="East"/>, <see cref="South"/> or <see cref="West"/>: the cell's own side when a cell is given.</param>
    /// <param name="cell">The cell of the edge to leave from, or <see langword="null"/> to leave from the nearest cell of the side.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when the map has no neighbour there, the cell is not on its edge or no cell of the side can be reached.</returns>
    public static async Task<MapChangeOutcome> LeaveAsync(this BotContext context, int direction, int? cell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var from = context.State.MapId ?? 0;
        long? to = null;
        var outcome = await LeaveByAsync(context, direction, cell, id => to = id, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new MapChangeEntry(at, depth, from, to, context.State.MapId, MapChangeKind.Scroll, outcome, took));

        return outcome;
    }

    /// <summary>Takes the door whose element stands on a cell, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="elementCell">The cell of the door's element in the map's data.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when no element of the map stands on the cell, the server offers it no skill, or no cell it is used from can be reached.</returns>
    public static async Task<MapChangeOutcome> TakeDoorAsync(this BotContext context, int elementCell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var from = context.State.MapId ?? 0;
        var outcome = await UseDoorAtAsync(context, elementCell, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new MapChangeEntry(at, depth, from, null, context.State.MapId, MapChangeKind.Door, outcome, took));

        return outcome;
    }

    /// <summary>Enters the character's own haven bag from the map it stands on, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when the character is in a haven bag already.</returns>
    public static async Task<MapChangeOutcome> EnterHavenBagAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var from = context.State.MapId ?? 0;
        var outcome = await EnterAsync(context, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new MapChangeEntry(at, depth, from, null, context.State.MapId, MapChangeKind.HavenBag, outcome, took));

        return outcome;
    }

    private static async Task<MapChangeOutcome> LeaveByAsync(BotContext context, int direction, int? cell, Action<long> chose, CancellationToken cancellationToken)
    {
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } here || state.IsBusy)
            return MapChangeOutcome.Busy;

        if (!context.Maps.TryGet(mapId, out var map))
            return MapChangeOutcome.NoPath;

        var cells = map.Cells.Span;
        var zone = cells[here].Zone;
        var exits = new List<int>();

        if (cell is { } chosen)
        {
            if (chosen is < 0 or >= MapData.CellCount || !cells[chosen].IsWalkable || cells[chosen].Zone != zone || SideOf(cells[chosen], chosen) is not { } side)
                return MapChangeOutcome.NoPath;

            direction = side;
            exits.Add(chosen);
        }
        else
        {
            for (var candidate = 0; candidate < MapData.CellCount; candidate++)
            {
                if (cells[candidate].IsWalkable && cells[candidate].Zone == zone && MapGeometry.Neighbour(candidate, direction) < 0 && MapGeometry.LeavesTowards(cells[candidate], candidate, direction))
                    exits.Add(candidate);
            }
        }

        var neighbour = NeighbourOf(map, direction);

        if (neighbour <= 0 || neighbour > int.MaxValue || exits.Count is 0)
            return MapChangeOutcome.NoPath;

        // Measured (9g5): the client does not follow a walk ending where its previous one did; another exit saves walking in two.
        if (exits.Any(exit => exit != state.LastWalkEnd))
            exits = [.. exits.Where(exit => exit != state.LastWalkEnd)];

        if (!CellPathfinder.TryFindNearest(cells, here, [.. exits], out var path))
            return MapChangeOutcome.NoPath;

        chose(neighbour);

        var outcome = await MapChange.ScrollAsync(context, mapId, path, (int)neighbour, cancellationToken).ConfigureAwait(false);

        return outcome is MapChangeOutcome.Changed && context.State.MapId != neighbour ? MapChangeOutcome.Elsewhere : outcome;
    }

    private static async Task<MapChangeOutcome> UseDoorAtAsync(BotContext context, int elementCell, CancellationToken cancellationToken)
    {
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } here || state.IsBusy)
            return MapChangeOutcome.Busy;

        if (!context.Maps.TryGet(mapId, out var map))
            return MapChangeOutcome.NoPath;

        var element = state.Elements.Values.FirstOrDefault(candidate => ElementUse.TryGetCell(map, candidate.ElementId, out var at) && at == elementCell);

        if (element is null)
            return MapChangeOutcome.NoPath;

        // A door's skill is the one the world's graph names; an element offering only another is taken for what it offers.
        if (!element.TryGetEnabled(WorldPathfinder.DoorSkillId, out var skill))
        {
            if (element.EnabledSkills.IsDefaultOrEmpty)
                return MapChangeOutcome.NoPath;

            skill = element.EnabledSkills[0];
        }

        var cells = map.Cells.Span;

        if (!CellPathfinder.TryFindNearest(cells, here, [.. ElementCells.UsedFrom(cells, elementCell, cells[here].Zone)], out var path))
            return MapChangeOutcome.NoPath;

        return await MapChange.UseDoorAsync(context, mapId, path, element.ElementId, skill.SkillId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MapChangeOutcome> EnterAsync(BotContext context, CancellationToken cancellationToken)
    {
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;
        var self = state.Key.CharacterId;

        if (state.MapId is not { } mapId || state.Cell is null || state.IsBusy)
            return MapChangeOutcome.Busy;

        if (state.IsInHavenBag)
            return MapChangeOutcome.NoPath;

        var (current, placed) = MapChange.ExpectNewMap(context, mapId);

        await context.SendAsync(new HavenBagEnterRequest { OwnerId = self }, cancellationToken).ConfigureAwait(false);

        return await MapChange.NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false);
    }

    // The straight side a cell of the edge leaves by, when it is on one.
    private static int? SideOf(MapCell mapCell, int cell)
    {
        foreach (var side in new[] { North, East, South, West })
        {
            if (MapGeometry.Neighbour(cell, side) < 0 && MapGeometry.LeavesTowards(mapCell, cell, side))
                return side;
        }

        return null;
    }

    private static long NeighbourOf(MapData map, int direction)
    {
        return direction switch
        {
            North => map.TopNeighbourId,
            East => map.RightNeighbourId,
            South => map.BottomNeighbourId,
            West => map.LeftNeighbourId,
            _ => 0,
        };
    }
}
