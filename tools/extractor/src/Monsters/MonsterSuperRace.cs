using D3B.Extractor.Texts;

namespace D3B.Extractor.Monsters;

/// <summary>A super race of monsters, races grouped by where they live, as <c>MonsterSuperRaceData</c> holds it.</summary>
internal sealed class MonsterSuperRace
{
    public required int Id { get; init; }

    public required Text Name { get; init; }
}
