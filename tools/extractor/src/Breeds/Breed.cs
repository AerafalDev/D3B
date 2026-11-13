using D3B.Extractor.Texts;

namespace D3B.Extractor.Breeds;

/// <summary>A breed, the character's class, such as the Iop, as <c>BreedData</c> holds it.</summary>
internal sealed class Breed
{
    public required int Id { get; init; }

    public required Text Name { get; init; }
}
