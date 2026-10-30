using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The fight's AI on fights the tests build: what it casts, where it walks, when it ends its turn, where it stands as the fight starts.</summary>
public sealed class FightTacticsTests
{
    private const int Blow = 100;
    private const int Heal = 200;
    private const int Bite = 300;

    private static readonly SpellCatalog _spells = Catalog();
    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(108, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 100, an earth blow of 10 to 12 on an enemy, 3 AP, range 1 to 4; spell 200, a fire heal of 30 on the caster,
    /// 2 AP, on its own cell; spell 300, a monster's earth bite of 25 to 30 on an enemy, 3 AP, range 1.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1100, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1200, SpellId = Heal, Grade = 1, ApCost = 2, MinRange = 0, Range = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1300, SpellId = Bite, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 108, TargetMask = 2, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 25, DiceSide = 30, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static Combatant Biter(long id, int cell, int life = 50)
    {
        return Enemy(id, cell) with { Life = life, ActionPoints = 3, MovementPoints = 3 };
    }

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Fight(Combatant actor, params Combatant[] enemies)
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, .. enemies], ActorId = actor.Id, Round = 1 };
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [Level(_spells, Blow), Level(_spells, Heal)]);

        foreach (var enemy in enemies)
            spells = spells.Add(enemy.Id, [Level(_spells, Bite)]);

        return (fight, new FightKnowledge { Spells = spells });
    }

    private static TurnPlan Plan(ImaginedFight fight, FightKnowledge knowledge)
    {
        return _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
    }

    [Fact]
    public void AnEnemyThatOneBlowKillsIsKilledRatherThanSpared()
    {
        // Measured live (D203): next turn's damage once outweighed a kill now, and the AI spared a fighter at 14 life.
        var (fight, knowledge) = Fight(Caster(), Biter(-1, Off(2, 0), life: 9));

        var plan = Plan(fight, knowledge);

        Assert.Contains(TacticAction.Cast(Blow, 1100, Off(2, 0)), plan.Actions);
        Assert.True(plan.Score > plan.Standing);
    }

    [Fact]
    public void AnEnemyOutOfRangeIsWalkedTowardsThenStruck()
    {
        var (fight, knowledge) = Fight(Caster(), Biter(-1, Off(6, 0)));

        var plan = Plan(fight, knowledge);
        var first = plan.Actions[0];

        Assert.Equal(TacticActionKind.Walk, first.Kind);
        Assert.True(MapGeometry.Distance(first.Cell, Off(6, 0)) <= 4);
        Assert.Equal(TacticActionKind.Cast, plan.Actions[1].Kind);
        Assert.Equal(Blow, plan.Actions[1].SpellId);
    }

    [Fact]
    public void ABadlyHurtCharacterHealsItself()
    {
        var (fight, knowledge) = Fight(Caster() with { Life = 20, MovementPoints = 0 }, Biter(-1, Off(12, 0)));

        var plan = Plan(fight, knowledge);

        Assert.Equal(TacticAction.Cast(Heal, 1200, Middle), plan.Actions[0]);
    }

    [Fact]
    public void ACharacterWithNothingWorthDoingEndsItsTurnAtOnce()
    {
        var (fight, knowledge) = Fight(Caster() with { ActionPoints = 1, MovementPoints = 0 }, Biter(-1, Off(8, 0)));

        Assert.True(Plan(fight, knowledge).IsEmpty);
    }

    [Fact]
    public void ACastTheServerRefusedIsNotWeighedAgainThatTurn()
    {
        var (fight, knowledge) = Fight(Caster() with { MovementPoints = 0 }, Biter(-1, Off(2, 0)));

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default, new HashSet<(int, int)> { (Blow, Off(2, 0)) });

        Assert.DoesNotContain(plan.Actions, action => action.SpellId is Blow);
    }

    [Fact]
    public void ACharacterIsPlacedWhereItReachesAnEnemyItBeats()
    {
        var near = Off(-3, 0);
        var far = Off(-8, 0);
        var (fight, knowledge) = Fight(Caster(Off(-9, 0)), Biter(-1, Off(2, 0), life: 20));

        Assert.Equal(near, _tactics.Place(fight, [far, near], knowledge, TacticWeights.Default));
    }

    [Fact]
    public void EnemiesThatHitHarderThanTheCharacterMakeTheFightRisky()
    {
        var (weak, weakKnowledge) = Fight(Caster(), Biter(-1, Off(4, 0), life: 20));
        var (strong, strongKnowledge) = Fight(Caster() with { Life = 40, MaxLife = 40 }, Biter(-1, Off(4, 0), life: 400), Biter(-2, Off(5, 0), life: 400));

        Assert.False(_tactics.Odds(weak, weakKnowledge).IsRisky);
        Assert.True(_tactics.Odds(strong, strongKnowledge).IsRisky);
    }

    [Fact]
    public void AWalkKeepsThePathTheReachableCellsKeep()
    {
        var (fight, _) = Fight(Caster(), Biter(-1, Off(6, 0)));

        var path = _tactics.PathTo(fight, Off(3, 0));

        Assert.Equal([Middle, Off(1, 0), Off(2, 0), Off(3, 0)], path);
        Assert.Empty(_tactics.PathTo(fight, Off(4, 0)));
    }
}
