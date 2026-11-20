using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A fork cast as the Forgelance's Trident de la Mer is, its second row striking past the cell cast on (D473).</summary>
public sealed class ForkTacticsTests
{
    private const int Trident = 50;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(96, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>Spell 50: 3 AP, cast 3 cells away exactly, striking 40 to 48 on the enemies of a fork of 1.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1000 + Trident, SpellId = Trident, Grade = 1, ApCost = 3, MinRange = 3, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 96, TargetMask = 0, Triggers = 1, DiceNum = 40, DiceSide = 48, Zone = Zone(SpellZoneShape.Fork) },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    [Fact]
    public void AForkIsCastWhereItsSecondRowStrikesAnEnemy()
    {
        // Measured (D473): Trident cast three cells ahead struck the monster two cells past the cell cast on.
        var actor = Caster() with { ActionPoints = 3, MovementPoints = 0, Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(0, 300), Level = 50 };
        var enemy = Enemy(-1, Off(5, 0)) with { Life = 300, MaxLife = 300 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = 1, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Trident)]).Add(enemy.Id, []) };

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        var cast = Assert.Single(plan.Actions, action => action.SpellId == Trident);
        Assert.Equal(Off(3, 0), cast.Cell);
    }
}
