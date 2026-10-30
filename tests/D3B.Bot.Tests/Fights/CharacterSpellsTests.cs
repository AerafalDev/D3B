using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Breeds;
using D3B.Data.Spells;

namespace D3B.Bot.Tests.Fights;

/// <summary>The spells the character casts: those the server listed, those its level unlocks, a pair's chosen spell, and the fight's own list.</summary>
public sealed class CharacterSpellsTests
{
    private const int Breed = 15;
    private const int Raised = 1;
    private const int Paired = 2;
    private const int Variant = 3;
    private const int Late = 4;
    private const int WeaponBlow = 0;

    private static readonly SpellCatalog _spells = Spells();
    private static readonly BreedCatalog _breeds = new(
        new[] { new Breed { Id = Breed, FirstSpell = 0, SpellCount = 4 } },
        new[] { Raised, Paired, Variant, Late },
        ReadOnlyMemory<BreedRole>.Empty,
        ReadOnlyMemory<BreedRoleValue>.Empty,
        ReadOnlyMemory<BreedStatCost>.Empty);

    /// <summary>
    /// Spell 1 at grade 1 from level 1 and grade 2 from level 20; spell 2 from level 10, paired with spell 3; spell 4 from level
    /// 50; spell 0, a weapon's blow.
    /// </summary>
    private static SpellCatalog Spells()
    {
        SpellLevel Level(int id, int spell, int grade, int least)
        {
            return new SpellLevel { Id = id, SpellId = spell, Grade = (byte)grade, MinPlayerLevel = (short)least, StatesCriterion = SpellLevel.NoCriterion };
        }

        SpellLevel[] levels = [Level(1, WeaponBlow, 1, 1), Level(11, Raised, 1, 1), Level(12, Raised, 2, 20), Level(21, Paired, 1, 10), Level(31, Variant, 1, 10), Level(41, Late, 1, 50)];
        Spell[] spells =
        [
            new Spell { Id = WeaponBlow, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Raised, FirstLevel = 1, LevelCount = 2 },
            new Spell { Id = Paired, FirstLevel = 3, LevelCount = 1 },
            new Spell { Id = Variant, FirstLevel = 4, LevelCount = 1 },
            new Spell { Id = Late, FirstLevel = 5, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), Array.Empty<SpellEffect>(), Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), Array.Empty<string>(),
            new[] { new SpellVariant(1, Breed, Paired, Variant) }, Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static int[] Of(ImmutableArray<SpellLevel> levels)
    {
        return [.. levels.Select(level => level.Id)];
    }

    [Fact]
    public void TheListedSpellsAreCastAtTheGradeTheLevelAllowsWithThoseItUnlocksButTheWeaponsBlow()
    {
        var levels = CharacterSpells.Of(new Dictionary<int, int> { [Raised] = 1, [WeaponBlow] = 1 }, Breed, 25, _spells, _breeds);

        Assert.Equal([12, 21], Of(levels));
    }

    [Fact]
    public void APairsSpellTheListHoldsTakesThePairsPlace()
    {
        var levels = CharacterSpells.Of(new Dictionary<int, int> { [Variant] = 1 }, Breed, 25, _spells, _breeds);

        Assert.Equal([12, 31], Of(levels));
    }

    [Fact]
    public void TheFightsListThenATransformationsGiveTheSpellsCastInAFight()
    {
        var character = new CharacterState { Key = TestPlay.Key, Name = "Character", Spells = ImmutableSortedDictionary<int, int>.Empty.Add(Raised, 1) };
        var self = new Fighter { Id = TestPlay.CharacterId, BreedId = Breed, Level = 60 };
        var fight = new FightState();

        Assert.Equal([12, 21, 41], Of(CharacterSpells.InFight(character, fight, self, _spells, _breeds)));
        Assert.Equal([21], Of(CharacterSpells.InFight(character with { TransformedSpells = ImmutableSortedDictionary<int, int>.Empty.Add(Paired, 1) }, fight, self, _spells, _breeds)));
        Assert.Equal([41], Of(CharacterSpells.InFight(character, fight with { Spells = ImmutableSortedDictionary<int, int>.Empty.Add(Late, 1) }, self, _spells, _breeds)));
    }
}
