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
    }), Monsters());

    /// <summary>
    /// Spell 40, a lance thrown as the pack's throws are: 3 AP, range 1 to 4, three times a turn, asking the lance in hand or state
    /// 3589, striking 40 to 48 in a cross of 1 and bringing the lance, monster 7139, to its free cell.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "HS=3360|HS=3589"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1000 + Throw, SpellId = Throw, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, MaxCastPerTurn = 3, StatesCriterion = 3, FirstEffect = 0, EffectCount = 2 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 2, DiceNum = 40, DiceSide = 48, Zone = Zone(SpellZoneShape.Cross) },
            new SpellEffect { Uid = 2, EffectId = 181, TargetMask = 1, Triggers = 2, DiceNum = Lance, DiceSide = 1, Zone = Zone(SpellZoneShape.Point) },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>Monster 7139, the lance, which plays and casts nothing.</summary>
    private static MonsterCatalog Monsters()
    {
        var grade = new MonsterGrade { Grade = 1, Level = 1, Characteristics = new MonsterCharacteristics(), Bonus = new MonsterCharacteristics { LifePoints = 10 } };
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
    public void AForgelanceThrowsItsLanceOnceATurn()
    {
        var actor = Thrower();
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 300, MaxLife = 300 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = 1, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Throw)]).Add(enemy.Id, []) };

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Single(plan.Actions, action => action.SpellId == Throw);
    }
}
