using D3B.Extractor.Texts;

namespace D3B.Extractor.Hints;

/// <summary>A hint of the world map, as <c>HintsData</c> holds it: what stands on a map, drawn with a picture.</summary>
internal sealed class Hint
{
    public required int Id { get; init; }

    public required int CategoryId { get; init; }

    /// <summary>Its picture's number, which tells apart what a category mixes: 401 a bank, 410 a zaap.</summary>
    public required int Gfx { get; init; }

    public required Text Name { get; init; }

    public required int MapId { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required bool Outdoor { get; init; }

    public required int SubAreaId { get; init; }

    /// <summary>The world the client draws it on, which for 36 hints of 3.7.13.17 is not its map's.</summary>
    public required int WorldMapId { get; init; }

    /// <summary>A dungeon's level, 0 for most others.</summary>
    public required uint Level { get; init; }
}
