using D3B.Extractor.Texts;

namespace D3B.Extractor.World;

/// <summary>A sub-area of the world, as <c>SubAreaData</c> holds it.</summary>
internal sealed class SubArea
{
    public required int Id { get; init; }

    public required Text? Name { get; init; }

    public required int AreaId { get; init; }

    public required uint Level { get; init; }

    /// <summary>Gets the map of the sub-area's zaap, or <see langword="null"/> when it has none.</summary>
    public required int? ZaapMapId { get; init; }

    public required bool BasicAccountAllowed { get; init; }

    /// <summary>Gets the world the sub-area is drawn on, -1 when it follows its area's.</summary>
    public required int CustomWorldMapId { get; init; }

    /// <summary>Gets the items its <c>harvestables</c> lists, as the client lists them: loot boxes among resources.</summary>
    public required int[] Harvestables { get; init; }

    /// <summary>Gets the monsters that live there, as the client lists them.</summary>
    public required int[] Monsters { get; init; }
}
