using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>What a strategy changes of the AI's search and placement (D234): spells preferred, spells left out, a placement near or far.</summary>
public sealed class StrategyTacticsTests
{
    private const int Blow = 100;
    private const int Heal = 200;
    private const int Critical = 500;

    private static readonly SpellCatalog _spells = Catalog();
    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(108, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 100, an earth blow of 10 to 12, 3 AP, range 1 to 4; spell 200, a fire heal of 30 on the caster, 2 AP; spell 500,
    /// an earth blow of 10, 30 on a critical hit, which half its casts are, 3 AP, range 1 to 4, worth 20 a cast against the
    /// first's 11.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1100, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1200, SpellId = Heal, Grade = 1, ApCost = 2, MinRange = 0, Range = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1500, SpellId = Critical, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, CriticalHitProbability = 50, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1, CriticalEffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 108, TargetMask = 2, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 10, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Fight(Combatant actor, Combatant enemy)
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = actor.Id, Round = 1 };
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty
            .Add(actor.Id, [Level(_spells, Blow), Level(_spells, Heal), Level(_spells, Critical)])
            .Add(enemy.Id, [Level(_spells, Blow)]);

        return (fight, new FightKnowledge { Spells = spells });
    }

    private static TurnPlan Plan(ImaginedFight fight, FightKnowledge knowledge, TacticWeights weights)
    {
        return _tactics.Plan(fight, knowledge, weights, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
    }

    [Fact]
    public void APreferredSpellIsCastWhereItBringsNearlyAsMuchAsAnother()
    {
        // One cast a turn: the critical blow brings 20, the plain one 11; preferred at twice what it brings, the plain one wins.
        var (fight, knowledge) = Fight(Caster() with { ActionPoints = 3, MovementPoints = 0 }, Enemy(-1, Off(2, 0)) with { Life = 200, MaxLife = 200 });

        var unbiased = Plan(fight, knowledge, TacticWeights.Default);
        var preferring = Plan(fight, knowledge, TacticWeights.Default with { Preferred = new[] { Blow }.ToFrozenSet(), Preference = 1 });

        Assert.Equal(Critical, unbiased.Actions.Single().SpellId);
        Assert.Equal(Blow, preferring.Actions.Single().SpellId);
    }

    [Fact]
    public void APreferredSpellNeverMakesAUselessCastWorthMaking()
    {
        // The caster is whole: its heal brings nothing, preferred or not, and no enemy is in reach.
        var (fight, knowledge) = Fight(Caster() with { MovementPoints = 0 }, Enemy(-1, Off(10, 0)));

        var plan = Plan(fight, knowledge, TacticWeights.Default with { Preferred = new[] { Heal }.ToFrozenSet(), Preference = 1 });

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void ASpellLeftOutOfTheFighterPlayingIsNeverCastTheEnemiesKeepingTheirs()
    {
        var (fight, knowledge) = Fight(Caster() with { ActionPoints = 3, MovementPoints = 0 }, Enemy(-1, Off(2, 0)) with { Life = 200, MaxLife = 200 });
        var without = knowledge.Without(fight.ActorId, new HashSet<int> { Critical });

        Assert.Equal(Blow, Plan(fight, without, TacticWeights.Default).Actions.Single().SpellId);
        Assert.Equal(knowledge.SpellsOf(-1), without.SpellsOf(-1));
        Assert.DoesNotContain(without.SpellsOf(fight.ActorId), level => level.SpellId == Critical);
    }

    [Fact]
    public void APlacementNearOrFarStandsByTheNearestEnemysDistanceFirst()
    {
        var nearest = Off(-1, 0);
        var middle = Off(-4, 0);
        var farthest = Off(-9, 0);
        var (fight, knowledge) = Fight(Caster(Off(-6, 0)), Enemy(-1, Off(2, 0)) with { Life = 20, MaxLife = 20 });
        int[] cells = [farthest, middle, nearest];

        Assert.Equal(nearest, _tactics.Place(fight, cells, knowledge, TacticWeights.Default, TacticPlacement.NearEnemies));
        Assert.Equal(farthest, _tactics.Place(fight, cells, knowledge, TacticWeights.Default, TacticPlacement.FarFromEnemies));
    }
}
