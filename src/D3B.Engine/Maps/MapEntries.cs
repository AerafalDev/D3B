using D3B.Data.Maps;

namespace D3B.Engine.Maps;

/// <summary>Where a character comes onto a map by a scroll, to stand on a part of it that its own part cannot walk to (D495).</summary>
/// <remarks>
/// Measured on the owner's mines: an ore on a ledge whose cliff cuts it from the rest of the map, floors counting on a map with
/// stairs (D86), is reached only by coming onto the map from the side the ledge touches. A scroll leaves the neighbouring map from a
/// cell of its edge and comes onto the cell straight across: 46,900 of the 52,658 scrolls of the captures of 3.7.14.18.
/// </remarks>
public static class MapEntries
{
    private const int RowWidth = 14;
    private const int AcrossRows = MapData.CellCount - (2 * RowWidth);

    /// <summary>Finds the cells of a map's edges from which a character walks to any of several cells.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="targets">The cells to walk to, such as those an element is used from.</param>
    /// <param name="zone">The zone the character walks in (<see cref="MapCell.Zone"/>): only its cells count.</param>
    /// <returns>By side, 0 east, 2 south, 4 west and 6 north, the walkable cells of that edge in the zone from which a path leads to a target; a side with none is left out.</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    public static IReadOnlyDictionary<int, IReadOnlyList<int>> Reaching(ReadOnlySpan<MapCell> cells, IReadOnlyList<int> targets, int zone)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        var reached = new SortedDictionary<int, IReadOnlyList<int>>();
        var goals = targets.ToArray();

        for (var side = 0; side < MapCell.DirectionCount; side += 2)
        {
            var edge = new List<int>();

            for (var cell = 0; cell < MapData.CellCount; cell++)
            {
                if (MapGeometry.Neighbour(cell, side) < 0 && cells[cell].IsWalkable && cells[cell].Zone == zone && CellPathfinder.TryFindNearest(cells, cell, goals, out _))
                    edge.Add(cell);
            }

            if (edge.Count > 0)
                reached[side] = edge;
        }

        return reached;
    }

    /// <summary>Finds the cell of the neighbouring map a scroll is left from to come onto a cell of a map's edge.</summary>
    /// <param name="cell">The cell of the map's edge.</param>
    /// <param name="side">The edge, 0 east, 2 south, 4 west or 6 north: the neighbouring map is on that side.</param>
    /// <returns>The cell straight across, on the neighbouring map's opposite edge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The side is not one of the four, or the cell does not exist.</exception>
    public static int LeftFrom(int cell, int side)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cell);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cell, MapData.CellCount);

        return side switch
        {
            0 => cell - (RowWidth - 1),
            2 => cell - AcrossRows,
            4 => cell + (RowWidth - 1),
            6 => cell + AcrossRows,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "A side is 0, 2, 4 or 6."),
        };
    }
}
