namespace D3B.Extractor.Maps;

/// <summary>One map of the client: its neighbours, its 560 cells as columns, and its interactive elements (docs/data.md).</summary>
internal sealed class MapData
{
    /// <summary>The number of cells of every map: 14 columns of 40 staggered rows (<c>main:docs/client.md</c>, "Cells").</summary>
    public const int CellCount = 560;

    public required int Id { get; init; }

    public required int Top { get; init; }

    public required int Bottom { get; init; }

    public required int Left { get; init; }

    public required int Right { get; init; }

    /// <summary>Gets each cell's booleans, one bit each in the order of <see cref="CellFlags.Names"/>.</summary>
    public required ushort[] Flags { get; init; }

    /// <summary>Gets each cell's <c>linkedZone</c>: its high four bits the cell's roleplay zone, which the world graph names.</summary>
    public required byte[] LinkedZone { get; init; }

    /// <summary>Gets the cells' other numbers, by field and then by cell, each only where it is not zero.</summary>
    public required IReadOnlyDictionary<string, SortedDictionary<int, int>> Numbers { get; init; }

    /// <summary>Gets the interactive elements, in order of id: each its cell, or -1 for none, and its graphic.</summary>
    public required IReadOnlyList<(int Id, int CellId, int GfxId)> Elements { get; init; }
}
