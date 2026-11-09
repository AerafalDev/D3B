using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>What the AI casts when a life steal, a plain blow and a push compete for the same AP (D407).</summary>
public sealed class LifeStealTacticsTests
{
    private const int Steal = 100;
    private const int Blow = 200;
    private const int Push = 300;

    private const int StealEffect = 92;
    private const int PushEffect = 5;
    private const int EarthEffect = 97;

    private static readonly SpellCatalog _spells = Catalog();
    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(PushEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(StealEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(EarthEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 100, an earth life steal of 8, which heals 4, 3 AP, range 1 to 4; spell 200, an earth blow of 12 on the same terms; spell
    /// 300, a push of 4 cells, 1 AP, range 1 to 4. Effect 97 is the blow's: its dice are those of the level the test sets.
    /// </summary>
    private static SpellCatalog Catalog(int blow = 12)
    {
        string[] strings = ["A", "I", "a"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1100, SpellId = Steal, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1200, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1300, SpellId = Push, Grade = 1, ApCost = 1, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = StealEffect, TargetMask = 0, Triggers = 1, DiceNum = 8, DiceSide = 8, Zone = point },
            new SpellEffect { Uid = 2, EffectId = EarthEffect, TargetMask = 0, Triggers = 1, DiceNum = blow, DiceSide = blow, Zone = point },
            new SpellEffect { Uid = 3, EffectId = PushEffect, TargetMask = 0, Triggers = 1, DiceNum = 4, DiceSide = 0, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static FightTactics Tactics(int blow)
    {
        var spells = Catalog(blow);

        return new FightTactics(spells, States(), new EffectCatalog(new Effect[]
        {
            new(PushEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
            new(StealEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
            new(EarthEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));
    }

    private static int[] Plan(int blow, int life, int enemyLife, int actionPoints, params int[] walls)
    {
        var tactics = Tactics(blow);
        var spells = tactics.Blows.Spells;
        var actor = Caster() with { Life = life, ActionPoints = actionPoints, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = enemyLife, MaxLife = Math.Max(enemyLife, 400), ActionPoints = 3, MovementPoints = 0 };
        var fight = new ImaginedFight { Map = Open(walls), Fighters = [actor, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [Level(spells, Steal), Level(spells, Blow), Level(spells, Push)]).Add(enemy.Id, []),
        };

        return [.. tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) }).Actions.Select(action => action.SpellId)];
    }

    [Fact]
    public void AHealthyCharacterCastsTheBlowThatDealsAThirdMoreRatherThanALifeSteal()
    {
        // Reported by players: a steal of 8 healing 4 was cast with the character's life almost full, over a blow of 12.
        Assert.Equal([Blow], Plan(blow: 12, life: 95, enemyLife: 400, actionPoints: 3));
    }

    [Fact]
    public void AHealthyCharacterKeepsTheLifeStealThatDealsAboutAsMuchAsTheBlow()
    {
        Assert.Equal([Steal], Plan(blow: 9, life: 80, enemyLife: 400, actionPoints: 3));
    }

    [Fact]
    public void AnInjuredCharacterStealsLifeThoughTheBlowDealsMore()
    {
        Assert.Equal([Steal], Plan(blow: 12, life: 30, enemyLife: 400, actionPoints: 3));
    }

    [Fact]
    public void OneBlowThatKillsIsCastRatherThanTwoLifeStealsThatKillAndHealAFewPoints()
    {
        // Reported by players: missing 3 life, the AI stole life twice from a monster a blow killed in one cast.
        Assert.Equal([Blow], Plan(blow: 20, life: 97, enemyLife: 16, actionPoints: 6));
    }

    [Fact]
    public void ABlowThatDealsMoreIsCastRatherThanAPushAgainstAWall()
    {
        // The push against the wall deals its 4 cells at level 0, 32 life: the judge weighs the damage of both.
        Assert.Equal([Blow], Plan(blow: 100, life: 100, enemyLife: 400, actionPoints: 3, Off(3, 0)));
    }

    [Fact]
    public void APushAgainstAWallThatDealsMoreIsCastRatherThanTheBlow()
    {
        Assert.Contains(Push, Plan(blow: 8, life: 100, enemyLife: 400, actionPoints: 3, Off(3, 0)));
    }

    [Fact]
    public void ALifePointAboveTheComfortableShareIsWorthLessThanOneBelowIt()
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [Caster(), Enemy(-1, Off(8, 0))], ActorId = 1, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty };
        var judge = _tactics.JudgeOf(fight, knowledge, TacticWeights.Default);

        double Worth(int life)
        {
            return judge.AlliesWorth(fight.With(Caster() with { Life = life }));
        }

        Assert.True(Worth(100) - Worth(90) < (Worth(40) - Worth(30)) / 2);
        Assert.True(Worth(100) > Worth(90));
    }
}
