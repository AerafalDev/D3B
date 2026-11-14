using D3B.Extractor.Texts;

namespace D3B.Extractor.Items;

/// <summary>An item, anything a character can carry, as <c>ItemData</c> and <c>WeaponData</c> hold it.</summary>
internal sealed class Item
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    public required int TypeId { get; init; }

    public required int Level { get; init; }

    public required int IconId { get; init; }
}
