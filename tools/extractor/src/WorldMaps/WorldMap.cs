using D3B.Extractor.Texts;

namespace D3B.Extractor.WorldMaps;

/// <summary>A world of the client's map, as <c>WorldMapsDataRoot</c> holds it, with the levels of zoom its tiles cut.</summary>
internal sealed class WorldMap
{
    /// <summary>The size of a tile's side, the last column and row cropped to what is left (<c>main:docs/client.md</c>).</summary>
    public const int TileSize = 1024;

    public required int Id { get; init; }

    public required Text? Name { get; init; }

    /// <summary>Gets where map 0, 0 begins on the image at zoom 100, in pixels.</summary>
    public required int OriginX { get; init; }

    public required int OriginY { get; init; }

    /// <summary>Gets a map's width on the image at zoom 100, in pixels.</summary>
    public required float MapWidth { get; init; }

    public required float MapHeight { get; init; }

    public required float MinScale { get; init; }

    public required float MaxScale { get; init; }

    public required float StartScale { get; init; }

    /// <summary>Gets the image's width at zoom 100, as the table gives it.</summary>
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required bool Visible { get; init; }

    /// <summary>Gets the levels of zoom, largest first, each with the size its tiles add up to.</summary>
    public required IReadOnlyList<(int Percent, int Width, int Height)> Zooms { get; init; }
}
