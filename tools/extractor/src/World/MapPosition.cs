using D3B.Extractor.Texts;

namespace D3B.Extractor.World;

/// <summary>Where a map stands in the world, as <c>MapInformationData</c> holds it.</summary>
internal sealed class MapPosition
{
    public required uint Id { get; init; }

    public required short X { get; init; }

    public required short Y { get; init; }

    public required int SubAreaId { get; init; }

    /// <summary>Gets the world whose image the map is drawn on, -1 for none (<c>main:docs/client.md</c>, "The world's maps").</summary>
    public required int WorldMap { get; init; }

    /// <summary>Gets the map's own name, which most maps lack.</summary>
    public required Text? Name { get; init; }

    /// <summary>Gets the names of the bits its <c>m_flags</c> sets, as <c>MapInformationFlags</c> names them (ADR 0010).</summary>
    public required IReadOnlyList<string> Flags { get; init; }
}
