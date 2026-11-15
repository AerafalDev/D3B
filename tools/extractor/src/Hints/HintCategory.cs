using D3B.Extractor.Texts;

namespace D3B.Extractor.Hints;

/// <summary>A category of the world map's hints, as <c>HintCategoryData</c> holds it, such as Transports.</summary>
internal sealed class HintCategory
{
    public required int Id { get; init; }

    public required Text Name { get; init; }
}
