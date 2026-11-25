using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A wild mount tamed for its capture, and never killed before (D533).</summary>
public sealed class MountTamingTacticsTests
{
    private const int Taming = 10;
    private const int Strike = 40;
    private const int Captured = 7109;
    private const long Mount = -1;
    private const long Other = -2;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 10, as the client 3.7.14.18 holds 32260 "Apprivoisement de monture": 1 AP at a range of 1 to 7 on an enemy, putting
    /// it in state 7109 "Monture capturée", cast once a fight; its using the net and the mount's colour states left out. Spell
    /// 40, 4 AP at a range of 1 to 4: an earth blow of 40.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1010, SpellId = Taming, Grade = 1, ApCost = 1, MinRange = 1, Range = 7, MinCastInterval = 63, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1040, SpellId = Strike, Grade = 1, ApCost = 4, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { EffectId = 950, TargetMask = 0, Triggers = 1, Value = Captured, Duration = -1, Zone = point },
            new SpellEffect { EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 40, DiceSide = 40, Zone = point },
        ];
        Spell[] spells =
        [
            new Spell { Id = Taming, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Strike, NameId = 2, FirstLevel = 1, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(Combatant character, params Combatant[] enemies)
    {
        return new ImaginedFight { Map = Open(), Fighters = [character, .. enemies], ActorId = character.Id, Round = 1 };
    }

    private static FightKnowledge Knowledge(Combatant character, params int[] spells)
    {
        return new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(character.Id, [.. spells.Select(spell => Level(_spells, spell))]) };
    }

    private static TamingPlan Tamed(ImaginedFight fight, FightKnowledge knowledge)
    {
        return _tactics.Taming(fight, knowledge, new HashSet<int> { Taming }, new HashSet<long> { Mount });
    }

    [Fact]
    public void AMountWithinTheSpellsRangeIsTamedFromWhereTheCharacterStands()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(5, 0)));

        var tamed = Tamed(fight, Knowledge(character, Taming, Strike));

        Assert.Equal([TacticAction.Cast(Taming, 1010, Off(5, 0))], tamed.Actions);
        Assert.True(tamed.IsPending);
    }

    [Fact]
    public void TheCharacterWalksWithinTheSpellsRangeOfAMountFartherAway()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(9, 0)));

        var tamed = Tamed(fight, Knowledge(character, Taming));

        Assert.Equal(2, tamed.Actions.Length);
        Assert.Equal(TacticActionKind.Walk, tamed.Actions[0].Kind);
        Assert.Equal(TacticAction.Cast(Taming, 1010, Off(9, 0)), tamed.Actions[1]);
        Assert.InRange(Engine.Maps.MapGeometry.Distance(tamed.Actions[0].Cell, Off(9, 0)), 1, 7);
    }

    [Fact]
    public void AMountBeyondTheTurnsReachIsLeftForALaterTurn()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(14, 0)));

        var tamed = Tamed(fight, Knowledge(character, Taming));

        Assert.Empty(tamed.Actions);
        Assert.True(tamed.IsPending);
    }

    [Fact]
    public void ACharacterWithoutTheTamingSpellTamesNothing()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(5, 0)));

        var tamed = Tamed(fight, Knowledge(character, Strike));

        Assert.Empty(tamed.Actions);
        Assert.False(tamed.IsPending);
    }

    [Fact]
    public void ATamingSpellWaitingItsIntervalTamesNothingMore()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(5, 0)));

        var tamed = Tamed(fight, Knowledge(character, Taming) with { Cooldowns = ImmutableDictionary<int, int>.Empty.Add(Taming, 62) });

        Assert.Empty(tamed.Actions);
        Assert.False(tamed.IsPending);
    }

    [Fact]
    public void AMountNoLongerAliveIsNoTarget()
    {
        var character = Caster();
        var fight = Fight(character, Enemy(Mount, Off(5, 0)) with { IsAlive = false });

        var tamed = Tamed(fight, Knowledge(character, Taming));

        Assert.Empty(tamed.Actions);
        Assert.False(tamed.IsPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATurnNeverKillsAMountSparedForItsCapture(bool spared)
    {
        // The blow kills the weak mount at once; another enemy within reach is struck instead while the mount is spared.
        var character = Caster() with { MovementPoints = 0 };
        var fight = Fight(character, Enemy(Mount, Off(2, 0)) with { Life = 30 }, Enemy(Other, Off(-2, 0)) with { Life = 300, MaxLife = 300 });
        var knowledge = Knowledge(character, Strike) with { Spared = spared ? [Mount] : [] };

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal(spared ? Off(-2, 0) : Off(2, 0), Assert.Single(plan.Actions).Cell);
    }
}
