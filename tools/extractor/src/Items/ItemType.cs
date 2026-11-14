using D3B.Extractor.Texts;

namespace D3B.Extractor.Items;

/// <summary>A type of item, such as wood, as <c>ItemTypeData</c> holds it.</summary>
internal sealed class ItemType
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    /// <summary>Gets the inventory's tab its items are shown in, 0 to 5 on 3.7.13.17, guessed from its types' names only.</summary>
    public required int CategoryId { get; init; }
}
