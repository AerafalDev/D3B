using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A blow that strikes an ally of the character costs what it takes from the ally, whatever that ally's life.</summary>
public sealed class AllyStrikeTacticsTests
{
    private const int Blow = 200;
    private const int Splash = 201;
    private const int Push = 202;
    private const int EarthEffect = 97;
    private const int PushEffect = 5;

    private static readonly FightTactics _tactics = new(Catalog(), States(), new EffectCatalog(new Effect[]
    {
        new(PushEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(EarthEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    public static TheoryData<string> Weights => ["Default", "Group", "Prudent"];

    /// <summary>Spell 200, an earth blow of 20 on an enemy; 201, one of 30 on every fighter of a cross about the fighter cast on, as an Enutrof's 13335 strikes; 202, a push of 2 cells, as an Iop's 13108 pushes; each 3 AP, range 1 to 4.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a", "a,A"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1200, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1201, SpellId = Splash, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, Properties = SpellLevelProperties.NeedTakenCell, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1202, SpellId = Push, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = EarthEffect, TargetMask = 0, Triggers = 1, DiceNum = 20, DiceSide = 20, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 2, EffectId = EarthEffect, TargetMask = 3, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = Zone(SpellZoneShape.Cross) with { DamageDecreaseStepPercent = 0 } },
            new SpellEffect { Uid = 3, EffectId = PushEffect, TargetMask = 3, Triggers = 1, DiceNum = 2, DiceSide = 0, Zone = Zone(SpellZoneShape.Point) },
        ];
        Spell[] spells =
        [
            new Spell { Id = Blow, NameId = Blow, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Splash, NameId = Splash, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Push, NameId = Push, FirstLevel = 2, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static TacticWeights Named(string name)
    {
        return name switch
        {
            "Group" => TacticWeights.Group,
            "Prudent" => TacticWeights.Prudent,
            _ => TacticWeights.Default,
        };
    }

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Beside(int allyLife)
    {
        // The enemy two cells away, the ally right behind it, within the splash's cross; nobody but the character can act.
        var actor = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var ally = Caster(Off(3, 0)) with { Id = 2, BreedId = 3, Life = allyLife, ActionPoints = 0, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 80, MaxLife = 100, ActionPoints = 0, MovementPoints = 0 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, ally, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty
                .Add(actor.Id, [Level(_tactics.Blows.Spells, Blow), Level(_tactics.Blows.Spells, Splash)]).Add(ally.Id, []).Add(enemy.Id, []),
        };

        return (fight, knowledge);
    }

    [Theory]
    [MemberData(nameof(Weights))]
    public void TheCharacterStrikesTheEnemyAloneRatherThanAlsoItsAllyAtFullLife(string weights)
    {
        // Measured on the owner's group runs of 2026-11-20 and 21: an Enutrof's 13335 took 6,162 life from the other
        // characters in 192 blows. Above 60% of its life an ally's point was worth a quarter of the curve's, 0.16, where a
        // point dealt an enemy is worth 0.6 to 1: a splash of 30 on the enemy and its ally beat a blow of 20 on the enemy.
        var (fight, knowledge) = Beside(allyLife: 100);

        var plan = _tactics.Plan(fight, knowledge, Named(weights), TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Blow], plan.Actions.Select(action => action.SpellId));
    }

    [Theory]
    [MemberData(nameof(Weights))]
    public void TheCharacterStrikesTheEnemyAloneRatherThanAlsoItsWoundedAlly(string weights)
    {
        var (fight, knowledge) = Beside(allyLife: 40);

        var plan = _tactics.Plan(fight, knowledge, Named(weights), TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Blow], plan.Actions.Select(action => action.SpellId));
    }

    [Theory]
    [MemberData(nameof(Weights))]
    public void TheCharacterSplashesTheEnemyWhenNoAllyStandsInTheZone(string weights)
    {
        var (fight, knowledge) = Beside(allyLife: 100);
        var away = fight with { Fighters = [.. fight.Fighters.Select(fighter => fighter.Id is 2 ? fighter with { Cell = Off(-3, 0) } : fighter)] };

        var plan = _tactics.Plan(away, knowledge, Named(weights), TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Splash], plan.Actions.Select(action => action.SpellId));
    }

    [Fact]
    public void TheFighterThatStopsAPushTakesHalfItsOwnPushDamage()
    {
        // Measured on the 213 captures of 3.7.14.18: the fighter right behind a target a push stopped took half the push
        // damage the formula gives against itself, its own reduction counted, 932 times in 1,012 on the caster's other side and
        // 692 in 756 on its own.
        var (fight, _) = Beside(allyLife: 100);
        var resisting = fight.With(fight.Find(2)! with { Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(FightCharacteristic.PushDamageReduction, 16) });

        var pushed = _tactics.Imagination.Cast(resisting, Level(_tactics.Blows.Spells, Push), Off(2, 0));

        // A push 2 cells short at level 0: 2 * 32 / 4 on the enemy; 2 * (32 - 16) / 4, halved, on the ally behind, the side's own blow.
        Assert.Equal((64, 96, 4.0), (pushed.Find(-1)!.Life, pushed.Find(2)!.Life, pushed.StruckBySide[2]));
    }
}
