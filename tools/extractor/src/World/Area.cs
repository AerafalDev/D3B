using D3B.Extractor.Texts;

namespace D3B.Extractor.World;

/// <summary>An area of the world, such as Astrub, as <c>AreaData</c> holds it.</summary>
internal sealed class Area
{
    public required int Id { get; init; }

    public required Text? Name { get; init; }

    public required int SuperAreaId { get; init; }

    /// <summary>Gets the world the area is drawn on when <see cref="HasWorldMap"/>; 0 for most (<c>main:docs/client.md</c>).</summary>
    public required int WorldmapId { get; init; }

    public required bool HasWorldMap { get; init; }
}
