using D3B.Extractor.Texts;

namespace D3B.Extractor.Monsters;

/// <summary>A monster, such as the Bouftou, as <c>MonsterData</c> holds it.</summary>
internal sealed class Monster
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    /// <summary>Gets its race, its family as a player names it, such as the bouftous.</summary>
    public required int RaceId { get; init; }

    /// <summary>Gets the names of the bits of <c>MonsterFlags</c> it sets, such as <c>isBoss</c> (ADR 0010).</summary>
    public required IReadOnlyList<string> Flags { get; init; }

    /// <summary>Gets the level of each of its grades, grade 1 first: 1 to 20 grades, not always rising (3.7.13.17).</summary>
    public required IReadOnlyList<int> Levels { get; init; }

    public required IReadOnlyList<MonsterDrop> Drops { get; init; }
}
