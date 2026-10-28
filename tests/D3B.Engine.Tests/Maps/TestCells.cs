using D3B.Data.Maps;

namespace D3B.Engine.Tests.Maps;

/// <summary>The cells of a map a test draws: walkable or not, nothing else.</summary>
internal static class TestCells
{
    private static readonly MapCell _walkable = new(MapCellProperties.Walkable | MapCellProperties.LineOfSight, 0, 0, 0, 0, 1 << 4, 0);
    private static readonly MapCell _blocked = new(MapCellProperties.None, 0, 0, 0, 0, 0, 0);

    /// <summary>A map whose every cell may be walked on but the ones listed.</summary>
    public static MapCell[] OpenBut(params int[] blocked)
    {
        return Where(cell => !blocked.Contains(cell));
    }

    /// <summary>A map whose cells may be walked on where a predicate says so.</summary>
    public static MapCell[] Where(Func<int, bool> walkable)
    {
        return [.. Enumerable.Range(0, MapData.CellCount).Select(cell => walkable(cell) ? _walkable : _blocked)];
    }

    /// <summary>A map whose cells may be walked on as a list says, by identifier.</summary>
    public static MapCell[] Of(bool[] walkable)
    {
        return Where(cell => walkable[cell]);
    }
}
