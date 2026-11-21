using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The Forgelance's lance as the AI imagines it: out of its hand once thrown, back in it as its next turn begins (D465).</summary>
public sealed class LanceTacticsTests
{
    private const int Throw = 40;
    private const int Collapse = 41;
    private const int Shield = 42;
    private const int Epilogue = 43;
    private const int Reach = 24591;
    private const int AroundLance = 51;
    private const int Lance = 7139;
    private const int InHand = 3360;
    private const int Out = 3361;
    private const int Carried = 3590;
    private const int Forgelance = 20;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(181, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(314, 0, 0, '-', 3, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1160, 0, 0, '/', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2905, 0, 0, ' ', 3, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2906, 0, 0, ' ', 3, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), Monsters());

    /// <summary>
    /// Spell 40, a lance thrown as the pack's throws are: 3 AP, range 1 to 4, three times a turn, asking the lance in hand or state
    /// 3589, striking 40 to 48 in a cross of 1 and bringing the lance, monster 7139, to its free cell. The lance's starting spell, 24591
    /// here as the one it casts in 3.7.14.18, gives its thrower's spells 41 and 42 a range of 1 to 63 and asks 41 a taken cell (D489). Spell
    /// 41, as Effondrement: 3 AP, range 0 to 1, its caster casting spell 51 on its own lance while it is out, which strikes 30 to 34
    /// in a cross of 1 around it. Spell 43, as Épilogue: 1 AP, bringing the lance only while it is in hand.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "HS=3360|HS=3589", "h,P", "a,P,F7139,*E3361", "a,A,*E3360"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1000 + Throw, SpellId = Throw, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, MaxCastPerTurn = 3, StatesCriterion = 3, FirstEffect = 0, EffectCount = 2 },
            new SpellLevel { Id = 1000 + Collapse, SpellId = Collapse, Grade = 1, ApCost = 3, MinRange = 0, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1000 + Shield, SpellId = Shield, Grade = 1, ApCost = 3, MinRange = 0, Range = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 0 },
            new SpellLevel { Id = 1000 + Epilogue, SpellId = Epilogue, Grade = 1, ApCost = 1, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, Properties = SpellLevelProperties.NeedFreeCell, FirstEffect = 3, EffectCount = 1 },
            new SpellLevel { Id = 1000 + AroundLance, SpellId = AroundLance, Grade = 1, Range = 63, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 4, EffectCount = 1 },
            new SpellLevel { Id = 1000 + Reach, SpellId = Reach, Grade = 1, Range = 63, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 5, EffectCount = 5 },
        ];
        var whole = Zone(SpellZoneShape.WholeMap, 63);
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 2, DiceNum = 40, DiceSide = 48, Zone = Zone(SpellZoneShape.Cross) },
            new SpellEffect { Uid = 2, EffectId = 181, TargetMask = 1, Triggers = 2, DiceNum = Lance, DiceSide = 1, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 3, EffectId = 1160, TargetMask = 5, Triggers = 2, DiceNum = AroundLance, DiceSide = 1, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 4, EffectId = 181, TargetMask = 6, Triggers = 2, DiceNum = Lance, DiceSide = 1, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 5, EffectId = 97, TargetMask = 0, Triggers = 2, DiceNum = 30, DiceSide = 34, Zone = Zone(SpellZoneShape.Cross) },
            new SpellEffect { Uid = 6, EffectId = 2905, TargetMask = 4, Triggers = 2, DiceNum = Shield, Value = 63, Duration = -1, Zone = whole },
            new SpellEffect { Uid = 7, EffectId = 2906, TargetMask = 4, Triggers = 2, DiceNum = Shield, Value = 1, Duration = -1, Zone = whole },
            new SpellEffect { Uid = 8, EffectId = 2905, TargetMask = 4, Triggers = 2, DiceNum = Collapse, Value = 63, Duration = -1, Zone = whole },
            new SpellEffect { Uid = 9, EffectId = 2906, TargetMask = 4, Triggers = 2, DiceNum = Collapse, Value = 1, Duration = -1, Zone = whole },
            new SpellEffect { Uid = 10, EffectId = 314, TargetMask = 4, Triggers = 2, DiceNum = Collapse, Value = 1, Duration = -1, Zone = whole },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>Monster 7139, the lance, which casts spell 24591 as it comes and plays nothing.</summary>
    private static MonsterCatalog Monsters()
    {
        var grade = new MonsterGrade { Grade = 1, Level = 1, StartingSpellLevelId = 1000 + Reach, Characteristics = new MonsterCharacteristics(), Bonus = new MonsterCharacteristics { LifePoints = 10 } };
        var monster = new Monster { Id = Lance, Properties = MonsterProperties.CanPlay, FirstGrade = 0, GradeCount = 1, FirstSpell = 0, SpellCount = 0 };

        return new MonsterCatalog(new[] { monster }, new[] { grade }, Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>());
    }

    private static Combatant Thrower()
    {
        return Caster() with { BreedId = Forgelance, States = [InHand, Carried], ActionPoints = 6, MovementPoints = 0, Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(0, 300), Level = 50 };
    }

    private static ImaginedFight Fight()
    {
        return new ImaginedFight { Map = Open(), Fighters = [Thrower(), Enemy(-1, Off(3, 0)) with { Life = 300, MaxLife = 300 }], ActorId = 1, Round = 1 };
    }

    private static ImaginedFight Thrown()
    {
        return _tactics.Imagination.Cast(Fight(), Level(_spells, Throw), Off(2, 0));
    }

    private static FightKnowledge Knowledge(ImaginedFight fight)
    {
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Throw)]);

        foreach (var fighter in fight.Fighters.Where(fighter => fighter.Id != 1))
            spells = spells.Add(fighter.Id, []);

        return new FightKnowledge { Spells = spells };
    }

    [Fact]
    public void AForgelanceThatThrewItsLanceNoLongerHoldsIt()
    {
        // Measured on the 83 captures of 18 and 19 November: at each throw the server ended states 3360 and 3590 and gave 3361.
        var thrower = Thrown().Actor;

        Assert.False(thrower.HasState(InHand));
        Assert.False(thrower.HasState(Carried));
        Assert.True(thrower.HasState(Out));
    }

    [Fact]
    public void ALanceOutOfHandCannotBeThrownAgainThatTurn()
    {
        var thrown = Thrown();

        Assert.Equal(CastRefusal.StatesForbid, _tactics.Rules.Check(thrown.Map, thrown.Fighters, thrown.Actor, Level(_spells, Throw), Off(4, 0), thrown.History, thrown.Round));
    }

    [Fact]
    public void TheLanceIsBackInHandAsItsThrowersNextTurnBegins()
    {
        // Measured: 304 of the Forgelance's 308 turns in the captures began with states 3360 and 3590 and no lance on the map.
        var thrown = Thrown();

        Assert.Contains(thrown.Fighters, fighter => fighter.MonsterId == Lance);

        var next = _tactics.Imagination.TurnStarted(thrown, 1, 2);

        Assert.True(next.Actor.HasState(InHand) && next.Actor.HasState(Carried));
        Assert.False(next.Actor.HasState(Out));
        Assert.DoesNotContain(next.Fighters, fighter => fighter.MonsterId == Lance);
    }

    [Fact]
    public void AnEnemysTurnLeavesTheLanceWhereItLies()
    {
        var thrown = Thrown();

        var next = _tactics.Imagination.TurnStarted(thrown, -1, 2);

        Assert.True(next.Find(1)!.HasState(Out));
        Assert.Contains(next.Fighters, fighter => fighter.MonsterId == Lance);
    }

    [Fact]
    public void TheJudgeCountsNextTurnsThrowsOnceTheLanceIsOut()
    {
        var fight = Fight();
        var judge = _tactics.JudgeOf(fight, Knowledge(fight), TacticWeights.Default);

        Assert.True(judge.NextTurnDamage(Thrown()) > 0);
    }

    [Fact]
    public void AFightersTurnBringsBackItsOwnLanceAlone()
    {
        // Two Forgelances, such as a character and an enemy's, each with its lance on the map.
        var other = Thrower() with { Id = 2, Team = 1, Cell = Off(-3, 0) };
        var fight = Fight() with { Fighters = [.. Fight().Fighters, other] };
        var both = _tactics.Imagination.Cast(_tactics.Imagination.Cast(fight, Level(_spells, Throw), Off(2, 0)) with { ActorId = 2 }, Level(_spells, Throw), Off(-2, 0));

        var next = _tactics.Imagination.TurnStarted(both, 1, 2);

        Assert.True(next.Find(1)!.HasState(InHand));
        Assert.True(next.Find(2)!.HasState(Out));
        Assert.Single(next.Fighters, fighter => fighter.MonsterId == Lance);
        Assert.Contains(next.Fighters, fighter => fighter.MonsterId == Lance && fighter.SummonerId == 2);
    }

    [Fact]
    public void AThrowWithoutTheLanceInHandLeavesTheStatesAsTheyWere()
    {
        // The throws ask 3360 or 3589: what a throw does to a thrower holding 3589 alone was never captured.
        var fight = Fight();
        var holder = fight.Actor with { States = [3589] };

        var thrown = _tactics.Imagination.Cast(fight.With(holder), Level(_spells, Throw), Off(2, 0));

        Assert.Equal([3589], thrown.Actor.States);
    }

    [Fact]
    public void AForgelanceThrowsItsLanceOnceATurn()
    {
        var actor = Thrower();
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 300, MaxLife = 300 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = 1, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Throw)]).Add(enemy.Id, []) };

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Single(plan.Actions, action => action.SpellId == Throw);
    }

    [Fact]
    public void AThrownLanceGivesEachSpellItNamesItsReach()
    {
        // Measured on the 25 captures of 20 and 21 November 2026 (D489): with the lance out, Effondrement was cast on it 43
        // times from beyond its own range, as the lance's spell 24591 sets the range of 13 of the Forgelance's spells.
        var thrower = Thrown().Actor;
        var collapse = CastRules.Modified(Level(_spells, Collapse), thrower);
        var shield = CastRules.Modified(Level(_spells, Shield), thrower);

        Assert.Equal((1, 63), (collapse.MinRange, collapse.Range));
        Assert.Equal(SpellLevelProperties.NeedTakenCell, collapse.Properties & SpellLevelProperties.NeedTakenCell);
        Assert.Equal((1, 63), (shield.MinRange, shield.Range));
    }

    [Fact]
    public void TheLanceTakesBackTheReachItGaveAsItReturns()
    {
        var next = _tactics.Imagination.TurnStarted(Thrown(), 1, 2);

        Assert.Equal((0, 1), (CastRules.Modified(Level(_spells, Collapse), next.Actor).MinRange, CastRules.Modified(Level(_spells, Collapse), next.Actor).Range));
        Assert.Empty(next.Actor.Modifiers);
    }

    [Fact]
    public void AForgelanceStrikesAroundItsLanceFromAfar()
    {
        // The owner's report of 21 November 2026: once its lance was down, the Forgelance cast its zone spell elsewhere than on it.
        var actor = Thrower();
        var enemy = Enemy(-1, Off(5, 0)) with { Life = 300, MaxLife = 300 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = 1, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Throw), Level(_spells, Collapse)]).Add(enemy.Id, []) };

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        var thrown = Assert.Single(plan.Actions, action => action.SpellId == Throw);

        Assert.Contains(plan.Actions, action => action.SpellId == Collapse && action.Cell == thrown.Cell);
    }

    [Fact]
    public void ASecondLanceDoesNotComeWhileTheFirstIsOut()
    {
        // Measured (D489): 15 casts of Épilogue on a free cell with the lance out brought nothing; its summon asks 3360 of its caster.
        var thrown = Thrown();

        var after = _tactics.Imagination.Cast(thrown, Level(_spells, Epilogue), Off(1, 1));

        Assert.Single(after.Fighters, fighter => fighter.MonsterId == Lance);
        Assert.Contains(_tactics.Imagination.Cast(Fight(), Level(_spells, Epilogue), Off(1, 1)).Fighters, fighter => fighter.MonsterId == Lance);
    }
}
