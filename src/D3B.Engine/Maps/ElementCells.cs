using D3B.Data.Maps;

namespace D3B.Engine.Maps;

/// <summary>The cells a character uses an interactive element of its map from, as the client chooses them.</summary>
/// <remarks>
/// Measured on the client's uses (client.md): it walks to a walkable neighbour of the element's cell, one of the eight,
/// never onto the element's cell, even when that can be walked on. When no neighbour can be walked on, as by water, it
/// walks to the walkable cell nearest the element (<see cref="MapGeometry.Distance"/>). The server accepted every one of
/// these, the client's and D3B's. An element whose walkable neighbours all lie in another zone of the map is out of
/// reach from this one: the nearest cell of the character's zone is no place to use it from (D424). On a map with stairs,
/// where floors count (D86), the server takes a use only from a neighbour on the element's floor: measured on the owner's
/// mines, it refused all 124 uses from a neighbour 100 to 190 above the ore and took 427 from 40 or less (D495).
/// </remarks>
public static class ElementCells
{
    // The most a use cell's floor was seen to differ from the element's and the server took it (D495); it refused from 100 up.
    private const int MostFloorGap = 40;

    /// <summary>Finds the cells of a zone that an element is used from.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="element">The element's cell.</param>
    /// <param name="zone">The zone the character walks in (<see cref="MapCell.Zone"/>): only its cells can be reached.</param>
    /// <returns>The walkable neighbours of the element's cell in the zone, on a map with stairs those on its floor when it has some; or, when it has no walkable neighbour in any zone, the zone's walkable cells nearest it; none when the zone has no walkable cell, or when the only walkable neighbours are in another zone.</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The element's cell does not exist.</exception>
    public static IReadOnlyList<int> UsedFrom(ReadOnlySpan<MapCell> cells, int element, int zone)
    {
        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        ArgumentOutOfRangeException.ThrowIfNegative(element);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(element, MapData.CellCount);

        var used = new List<int>();
        var neighbours = UseNeighbours(cells, element);

        foreach (var neighbour in neighbours)
        {
            if (cells[neighbour].Zone == zone)
                used.Add(neighbour);
        }

        if (used.Count > 0 || neighbours.Count > 0)
            return used;

        var nearest = int.MaxValue;

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            if (cell == element || !IsIn(cells[cell], zone))
                continue;

            var distance = MapGeometry.Distance(cell, element);

            if (distance > nearest)
                continue;

            if (distance < nearest)
            {
                nearest = distance;
                used.Clear();
            }

            used.Add(cell);
        }

        return used;
    }

    /// <summary>Lists the zones an element can be used from: those of its walkable neighbours.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="element">The element's cell.</param>
    /// <returns>The zones of the walkable neighbours of the element's cell it is used from (<see cref="UsedFrom"/>), each once, in ascending order; none when it has no walkable neighbour (water).</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The element's cell does not exist.</exception>
    public static IReadOnlyList<int> NeighbourZones(ReadOnlySpan<MapCell> cells, int element)
    {
        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        ArgumentOutOfRangeException.ThrowIfNegative(element);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(element, MapData.CellCount);

        var zones = new SortedSet<int>();

        foreach (var neighbour in UseNeighbours(cells, element))
            zones.Add(cells[neighbour].Zone);

        return [.. zones];
    }

    // The walkable neighbours an element is used from, in any zone: on a map with stairs, those on its floor when it has some.
    private static List<int> UseNeighbours(ReadOnlySpan<MapCell> cells, int element)
    {
        var walkable = new List<int>();

        for (var direction = 0; direction < MapCell.DirectionCount; direction++)
        {
            var neighbour = MapGeometry.Neighbour(element, direction);

            if (neighbour >= 0 && cells[neighbour].IsWalkable)
                walkable.Add(neighbour);
        }

        if (!CellPathfinder.HasStairs(cells))
            return walkable;

        var floor = cells[element].Floor;
        var level = new List<int>();

        foreach (var neighbour in walkable)
        {
            if (Math.Abs(cells[neighbour].Floor - floor) <= MostFloorGap)
                level.Add(neighbour);
        }

        return level.Count > 0 ? level : walkable;
    }

    private static bool IsIn(MapCell cell, int zone)
    {
        return cell.IsWalkable && cell.Zone == zone;
    }
}
