using D3B.Extractor.Texts;

namespace D3B.Extractor.Items;

/// <summary>A skill of a job, such as cutting an ash tree, as <c>SkillData</c> holds it.</summary>
internal sealed class Skill
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    public required int ParentJobId { get; init; }

    /// <summary>Gets the item it gathers, its <c>gatheredRessourceItem</c>, or <see langword="null"/> for none.</summary>
    public required int? GatheredItemId { get; init; }

    public required int LevelMin { get; init; }

    /// <summary>Gets the distance it is used from, in cells: 1, or 10 for fishing (measured on 3.7.12.12, inferred for 3.7.13.17, <c>artifacts/studies/items.md</c>).</summary>
    public required int Range { get; init; }

    /// <summary>Gets whether the client shows it: five level-1 harvest skills repeat another with false, such as 299 beside 6 (3.7.13.17, <c>artifacts/studies/items.md</c>).</summary>
    public required bool ClientDisplay { get; init; }
}
