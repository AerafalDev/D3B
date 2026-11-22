using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A turn keeps the fight's easy challenge while what it costs stays within what the challenge pays (D466).</summary>
public sealed class ChallengeTacticsTests
{
    private const int Blow = 200;
    private const int Other = 201;
    private const int EarthEffect = 97;

    private static readonly FightTactics _tactics = new(Catalog(), States(), new EffectCatalog(new Effect[]
    {
        new(EarthEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    private static readonly TurnSearchLimits _limits = TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) };

    /// <summary>Spells 200 and 201, each an earth blow of 20, 3 AP, range 1 to 4.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1200, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1201, SpellId = Other, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = EarthEffect, TargetMask = 0, Triggers = 1, DiceNum = 20, DiceSide = 20, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 2, EffectId = EarthEffect, TargetMask = 0, Triggers = 1, DiceNum = 20, DiceSide = 20, Zone = Zone(SpellZoneShape.Point) },
        ];
        Spell[] spells = [new Spell { Id = Blow, NameId = Blow, FirstLevel = 0, LevelCount = 1 }, new Spell { Id = Other, NameId = Other, FirstLevel = 1, LevelCount = 1 }];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Fight(int enemyCell, int movementPoints, params int[] spells)
    {
        var actor = Caster() with { MovementPoints = movementPoints };
        var enemy = Enemy(-1, enemyCell) with { Life = 200, MaxLife = 200, ActionPoints = 3, MovementPoints = 0 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [.. spells.Select(spell => Level(_tactics.Blows.Spells, spell))]).Add(enemy.Id, []),
            TurnPoints = ImmutableDictionary<long, (int, int)>.Empty.Add(actor.Id, (actor.ActionPoints, movementPoints)),
        };

        return (fight, knowledge);
    }

    private static int Walked(ImmutableArray<TacticAction> actions)
    {
        var at = Middle;
        var walked = 0;

        foreach (var action in actions.Where(action => action.Kind is TacticActionKind.Walk))
        {
            walked += MapGeometry.Distance(at, action.Cell);
            at = action.Cell;
        }

        return walked;
    }

    [Fact]
    public void WithNoChallengeThePlanKeepsNone()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 3, Blow);

        Assert.False(_tactics.Plan(fight, knowledge, TacticWeights.Default, _limits).KeepsChallenge);
    }

    [Fact]
    public void VersatileCastsTwoSpellsOnceRatherThanOneTwice()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 0, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("SAt0", 100));

        Assert.True(plan.KeepsChallenge);
        Assert.Equal([Blow, Other], plan.Actions.Where(action => action.Kind is TacticActionKind.Cast).Select(action => action.SpellId).Order());
    }

    [Fact]
    public void VersatileIsLeftWhenItHalvesTheTurnsDamage()
    {
        // One spell: keeping the challenge casts it once where the turn may cast it twice, half the turn's damage lost.
        var (fight, knowledge) = Fight(Off(3, 0), 0, Blow);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("SAt0", 130));

        Assert.False(plan.KeepsChallenge);
        Assert.Equal([Blow, Blow], plan.Actions.Where(action => action.Kind is TacticActionKind.Cast).Select(action => action.SpellId));
    }

    [Fact]
    public void ZombieWalksExactlyOneCell()
    {
        var (fight, knowledge) = Fight(Off(5, 0), 3, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("TM=1,0", 100));

        Assert.True(plan.KeepsChallenge);
        Assert.Equal(1, Walked(plan.Actions));
        Assert.Equal(2, plan.Actions.Count(action => action.Kind is TacticActionKind.Cast));
    }

    [Fact]
    public void NomadeUsesEveryMovementPoint()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 3, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("TM=m,0&TT=0,0", 100));

        Assert.True(plan.KeepsChallenge);
        Assert.Equal(3, Walked(plan.Actions));
        Assert.Equal(2, plan.Actions.Count(action => action.Kind is TacticActionKind.Cast));
    }

    [Fact]
    public void StatueIsLeftWhenStandingStillStrikesNothing()
    {
        // The enemy stands out of reach: keeping still, the turn does nothing.
        var (fight, knowledge) = Fight(Off(6, 0), 3, Blow);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("Tm<1,0", 130));

        Assert.False(plan.KeepsChallenge);
        Assert.Contains(plan.Actions, action => action.Kind is TacticActionKind.Walk);
    }

    [Fact]
    public void AChallengeTheTurnKeptSoFarIsKeptToItsEnd()
    {
        // The same turn, once a plan of it kept Statue: whatever is left of the turn to gain, the challenge is not left.
        var (fight, knowledge) = Fight(Off(6, 0), 3, Blow);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("Tm<1,0", 130)!.KeptThisTurn());

        Assert.True(plan.KeepsChallenge);
        Assert.DoesNotContain(plan.Actions, action => action.Kind is TacticActionKind.Walk);
    }

    [Fact]
    public void StatueIsKeptWhenTheEnemyIsInReach()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 3, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("Tm<1,0", 100));

        Assert.True(plan.KeepsChallenge);
        Assert.DoesNotContain(plan.Actions, action => action.Kind is TacticActionKind.Walk);
        Assert.Equal(2, plan.Actions.Count(action => action.Kind is TacticActionKind.Cast));
    }

    [Fact]
    public void AChallengeNoEndCanKeepLeavesTheBestPlan()
    {
        // Zombie with no movement point: no turn uses exactly one.
        var (fight, knowledge) = Fight(Off(3, 0), 0, Blow);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("TM=1,0", 100));

        Assert.False(plan.KeepsChallenge);
        Assert.Equal(2, plan.Actions.Count(action => action.Kind is TacticActionKind.Cast));
    }

    [Fact]
    public void PrudentEndsTheTurnOnACellBesideNoEnemy()
    {
        var (fight, knowledge) = Fight(Off(1, 0), 3, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("TD>1,hc0,e1", 100));
        var end = plan.Actions.Where(action => action.Kind is TacticActionKind.Walk).Select(action => action.Cell).DefaultIfEmpty(Middle).Last();

        Assert.True(plan.KeepsChallenge);
        Assert.True(MapGeometry.Distance(end, Off(1, 0)) > 1);
    }

    [Fact]
    public void HardiEndsTheTurnBesideAnEnemy()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 3, Blow, Other);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, _limits, challenge: TurnChallenge.Of("TD<2,hc0,e1", 100)!.KeptThisTurn());
        var end = plan.Actions.Where(action => action.Kind is TacticActionKind.Walk).Select(action => action.Cell).DefaultIfEmpty(Middle).Last();

        Assert.True(plan.KeepsChallenge);
        Assert.Equal(1, MapGeometry.Distance(end, Off(3, 0)));
    }

    [Fact]
    public void ElitisteStrikesOnlyItsTargetWhenAnotherEnemyIsEasierToKill()
    {
        var (fight, knowledge) = Fight(Off(3, 0), 0, Blow, Other);
        var weak = Enemy(-2, Off(0, 3)) with { Life = 30, MaxLife = 200, ActionPoints = 3, MovementPoints = 0 };
        var two = fight with { Fighters = [.. fight.Fighters, weak] };
        var elitist = TurnChallenge.Of("CK~!,1", 100)! with { TargetId = -1 };

        var free = _tactics.Plan(two, knowledge.With(weak.Id, []), TacticWeights.Default, _limits);
        var kept = _tactics.Plan(two, knowledge.With(weak.Id, []), TacticWeights.Default, _limits, challenge: elitist.KeptThisTurn());

        Assert.Contains(free.Actions, action => action.Kind is TacticActionKind.Cast && action.Cell == weak.Cell);
        Assert.True(kept.KeepsChallenge);
        Assert.All(kept.Actions.Where(action => action.Kind is TacticActionKind.Cast), action => Assert.Equal(Off(3, 0), action.Cell));
    }

    [Fact]
    public void TheBestPlanThatKillsAnotherEnemyFirstBreaksPremier()
    {
        // A challenge that pays nothing is never kept at a cost: the best plan, which kills the weak enemy, is played and judged.
        var (fight, knowledge) = Fight(Off(3, 0), 0, Blow, Other);
        var weak = Enemy(-2, Off(0, 3)) with { Life = 30, MaxLife = 200, ActionPoints = 3, MovementPoints = 0 };
        var two = fight with { Fighters = [.. fight.Fighters, weak] };

        var plan = _tactics.Plan(two, knowledge.With(weak.Id, []), TacticWeights.Default, _limits, challenge: TurnChallenge.Of("CK~1", 0)! with { TargetId = -1 });

        Assert.Contains(plan.Actions, action => action.Kind is TacticActionKind.Cast && action.Cell == weak.Cell);
        Assert.False(plan.KeepsChallenge);
    }
}
