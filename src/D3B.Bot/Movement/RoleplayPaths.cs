using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Movement;

/// <summary>The paths a character walks on a map out of fights: round the cells of the map's actions when it can (D485).</summary>
/// <remarks>
/// Measured on two runs' captures in a mine: the harvest's walk to an ore crossed the cell of the map's action, and the server
/// moved the character to the next map there, before the walk's end, every one of 61 times; the ore was never reached, and the
/// tour went round the mine for 18 minutes. A cell asked for, or the one the walk starts on, is no cell to go round; a walk that
/// cannot go round one crosses it, as it did before, its map change then answered as any other.
/// </remarks>
internal static class RoleplayPaths
{
    /// <summary>Finds the quickest path between two cells, round the map's action cells when it can.</summary>
    /// <param name="context">The behavior's context, holding the world's graph.</param>
    /// <param name="map">The map.</param>
    /// <param name="from">The cell the walk starts on.</param>
    /// <param name="to">The cell to walk to.</param>
    /// <param name="path">The path, when there is one.</param>
    /// <returns>Whether the character can walk there.</returns>
    public static bool TryFind(BotContext context, MapData map, int from, int to, [NotNullWhen(true)] out CellPath? path)
    {
        return TryFindNearest(context, map, from, [to], out path);
    }

    /// <summary>Finds the quickest path from a cell to the nearest of several, round the map's action cells when it can.</summary>
    /// <param name="context">The behavior's context, holding the world's graph.</param>
    /// <param name="map">The map.</param>
    /// <param name="from">The cell the walk starts on.</param>
    /// <param name="destinations">The cells to walk to any of.</param>
    /// <param name="path">The path, when there is one.</param>
    /// <returns>Whether the character can walk to any of them.</returns>
    public static bool TryFindNearest(BotContext context, MapData map, int from, ReadOnlySpan<int> destinations, [NotNullWhen(true)] out CellPath? path)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(map);

        var cells = map.Cells.Span;
        var avoided = context.World.ActionCells(map.Id);

        if (!avoided.IsDefaultOrEmpty && CellPathfinder.TryFindNearest(cells, from, destinations, avoided.AsSpan(), out path))
            return true;

        return CellPathfinder.TryFindNearest(cells, from, destinations, out path);
    }
}
