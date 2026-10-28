using System.Diagnostics.CodeAnalysis;
using D3B.Data.Maps;

namespace D3B.Engine.Maps;

/// <summary>Finds the quickest path between two cells of a map, outside fights, through the cells a character may walk on.</summary>
/// <remarks>
/// <para>
/// A character steps to any of the eight neighbours of its cell (<see cref="MapGeometry"/>) that it may walk on. A step
/// across, east, south, west or north, passes between two cells, one of which at least must be walkable: the client's
/// paths never passed between two that are not, although the server walked one that did (client.md).
/// </para>
/// <para>
/// Steps are weighed by how long the character takes, measured on the client's paths: running, about 170 ms a step east
/// or west, 110 north or south and 113 along a diagonal, so 3, 2 and 2. A* searches with the cost of the quickest path on
/// a map with nothing in the way, which never overestimates. Nothing is kept between calls: any number of bots find
/// paths at once.
/// </para>
/// </remarks>
public static class CellPathfinder
{
    private const int EastWestCost = 3;
    private const int NorthSouthCost = 2;
    private const int DiagonalCost = 2;

    /// <summary>Finds the quickest path between two cells.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="from">The cell the walk starts on: where the character stands, walkable or not.</param>
    /// <param name="to">The cell to walk to.</param>
    /// <param name="path">The path, when there is one: a single cell when both are the same.</param>
    /// <returns>Whether the character can walk there.</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static bool TryFind(ReadOnlySpan<MapCell> cells, int from, int to, [NotNullWhen(true)] out CellPath? path)
    {
        return TryFindNearest(cells, from, [to], out path);
    }

    /// <summary>Finds the quickest path from a cell to the nearest of several, such as the cells a map may be left from.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="from">The cell the walk starts on: where the character stands, walkable or not.</param>
    /// <param name="destinations">The cells to walk to any of; those that cannot be walked on are no destination, but the start.</param>
    /// <param name="path">The path to the one reached quickest, when there is one: a single cell when the start is one of them.</param>
    /// <returns>Whether the character can walk to any of them.</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static bool TryFindNearest(ReadOnlySpan<MapCell> cells, int from, ReadOnlySpan<int> destinations, [NotNullWhen(true)] out CellPath? path)
    {
        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        CheckCell(from, nameof(from));

        Span<bool> goal = stackalloc bool[MapData.CellCount];
        var goals = new List<MapPoint>(destinations.Length);

        path = null;

        foreach (var destination in destinations)
        {
            CheckCell(destination, nameof(destinations));

            if (destination == from)
            {
                path = new CellPath([from]);

                return true;
            }

            if (cells[destination].IsWalkable && !goal[destination])
            {
                goal[destination] = true;
                goals.Add(MapGeometry.PointOf(destination));
            }
        }

        if (goals.Count is 0)
            return false;

        Span<int> costs = stackalloc int[MapData.CellCount];
        Span<short> previous = stackalloc short[MapData.CellCount];
        Span<bool> closed = stackalloc bool[MapData.CellCount];
        var open = new PriorityQueue<int, int>();
        var reached = -1;

        costs.Fill(int.MaxValue);
        costs[from] = 0;
        open.Enqueue(from, Estimate(MapGeometry.PointOf(from), goals));

        while (open.TryDequeue(out var cell, out _))
        {
            if (goal[cell])
            {
                reached = cell;

                break;
            }

            if (closed[cell])
                continue;

            closed[cell] = true;

            for (var direction = 0; direction < MapCell.DirectionCount; direction++)
            {
                var next = MapGeometry.Neighbour(cell, direction);

                if (next < 0 || closed[next] || !cells[next].IsWalkable || !CanCross(cells, cell, direction))
                    continue;

                var cost = costs[cell] + CostOf(direction);

                if (cost >= costs[next])
                    continue;

                costs[next] = cost;
                previous[next] = (short)cell;
                open.Enqueue(next, cost + Estimate(MapGeometry.PointOf(next), goals));
            }
        }

        if (reached < 0)
            return false;

        var cellsOfPath = new List<int>();

        for (var cell = reached; cell != from; cell = previous[cell])
            cellsOfPath.Add(cell);

        cellsOfPath.Add(from);
        cellsOfPath.Reverse();
        path = new CellPath(cellsOfPath);

        return true;
    }

    private static void CheckCell(int cell, string name)
    {
        if (cell is < 0 or >= MapData.CellCount)
            throw new ArgumentOutOfRangeException(name, cell, $"A cell lies between 0 and {MapData.CellCount - 1}.");
    }

    private static bool CanCross(ReadOnlySpan<MapCell> cells, int cell, int direction)
    {
        if (!MapGeometry.IsAcross(direction))
            return true;

        var left = MapGeometry.Neighbour(cell, (direction + MapCell.DirectionCount - 1) % MapCell.DirectionCount);
        var right = MapGeometry.Neighbour(cell, (direction + 1) % MapCell.DirectionCount);

        return (left >= 0 && cells[left].IsWalkable) || (right >= 0 && cells[right].IsWalkable);
    }

    private static int CostOf(int direction)
    {
        return direction switch
        {
            0 or 4 => EastWestCost,
            2 or 6 => NorthSouthCost,
            _ => DiagonalCost,
        };
    }

    private static int Estimate(MapPoint from, List<MapPoint> goals)
    {
        // The nearest destination's estimate: still never more than the quickest path to any of them.
        var estimate = int.MaxValue;

        foreach (var goal in goals)
            estimate = Math.Min(estimate, Estimate(from, goal));

        return estimate;
    }

    private static int Estimate(MapPoint from, MapPoint to)
    {
        // A step east or west moves both coordinates the same way, north or south the opposite ways, a diagonal one of
        // them: with nothing in the way, the quickest path takes as many steps across as both coordinates move together.
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var both = Math.Min(Math.Abs(dx), Math.Abs(dy));
        var across = Math.Sign(dx) == Math.Sign(dy) ? EastWestCost : NorthSouthCost;

        // Each step across stands for two diagonals.
        return DiagonalCost * (Math.Abs(dx) + Math.Abs(dy)) - (2 * DiagonalCost - across) * both;
    }
}
