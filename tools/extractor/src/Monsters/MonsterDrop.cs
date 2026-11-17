namespace D3B.Extractor.Monsters;

/// <summary>An item a monster drops, as its <c>drops</c> list it: a monster may drop one item twice, under two criteria.</summary>
internal sealed class MonsterDrop
{
    public required int ItemId { get; init; }

    /// <summary>Gets its chances in percent for grades 1 to 5, the client's <c>percentDropForGrade1</c> to <c>5</c>, even for a monster of more grades or fewer (3.7.13.17).</summary>
    public required IReadOnlyList<float> Chances { get; init; }

    /// <summary>Gets the client's criterion, its <c>criterions</c> as written, such as <c>Pj&gt;41,9&amp;CU&gt;0</c> for a hunter's meat, or <see langword="null"/> for none; read by the bot in Rust.</summary>
    public required string? Criterion { get; init; }
}
