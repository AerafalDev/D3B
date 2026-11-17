using D3B.Extractor.Texts;

namespace D3B.Extractor.Monsters;

/// <summary>A race of monsters, a family as a player names it, such as the bouftous, as <c>MonsterRaceData</c> holds it.</summary>
internal sealed class MonsterRace
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    public required int SuperRaceId { get; init; }
}
