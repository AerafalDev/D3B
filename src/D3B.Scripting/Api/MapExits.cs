using System.Globalization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Scripting.Api;

/// <summary>Leaving the map as scripts name it: by a side, or from a cell, through the world graph's transitions and <see cref="MapChange"/>.</summary>
internal static class MapExits
{
    /// <summary>Reads a side as scripts name it, <c>right</c>, <c>bottom</c>, <c>left</c> or <c>top</c>, into the graph's direction.</summary>
    /// <remarks>The graph's directions run clockwise from east: a scroll is 0, 2, 4 or 6 (D27).</remarks>
    public static bool TryReadSide(string side, out int direction)
    {
        direction = side switch
        {
            "right" => 0,
            "bottom" => 2,
            "left" => 4,
            "top" => 6,
            _ => -1,
        };

        return direction >= 0;
    }

    /// <summary>Reads a cell as scripts write it, a whole number between 0 and 559, as text or not.</summary>
    public static bool TryReadCell(string text, out int cell)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out cell) && cell is >= 0 and < D3B.Data.Maps.MapData.CellCount;
    }

    /// <summary>Changes to the neighbour on one side, by the scroll the graph holds from the zone the character stands in.</summary>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when no scroll leaves that side.</returns>
    public static Task<MapChangeOutcome> BySideAsync(BotContext context, int direction, CancellationToken cancellationToken)
    {
        return ChangeAsync(context, transition => transition.Type is WorldTransitionType.Scroll or WorldTransitionType.ScrollAction && transition.Direction == direction, cancellationToken);
    }

    /// <summary>Changes map by the map action of a cell, the graph's transition stepped onto there.</summary>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when no map action starts from that cell.</returns>
    public static Task<MapChangeOutcome> FromCellAsync(BotContext context, int cell, CancellationToken cancellationToken)
    {
        return ChangeAsync(context, transition => transition.Type is WorldTransitionType.MapAction && transition.CellId == cell, cancellationToken);
    }

    private static Task<MapChangeOutcome> ChangeAsync(BotContext context, Func<WorldTransition, bool> fits, CancellationToken cancellationToken)
    {
        var state = context.State;
        var world = context.World;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || !context.Maps.TryGet(mapId, out var map) || !world.TryGetVertex(map, cell, out var vertex))
            return Task.FromResult(MapChangeOutcome.NoPath);

        var transitions = world.Graph.Transitions.Span;

        foreach (var edge in world.Graph.EdgesFrom(vertex))
        {
            for (var index = edge.FirstTransition; index < edge.FirstTransition + edge.TransitionCount; index++)
            {
                if (fits(transitions[index]))
                    return context.ChangeMapAsync(new WorldStep(edge.From, edge.To, index, transitions[index]), WorldTravel.RulesOf(context), cancellationToken);
            }
        }

        return Task.FromResult(MapChangeOutcome.NoPath);
    }
}
