using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A Sram's traps as the AI imagines them, as the references trigger them (D493).</summary>
public sealed class TrapTacticsTests
{
    private const int SetTrap = 50;
    private const int TrapStrike = 51;
    private const int Push = 52;
    private const int Shot = 53;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(5, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 0, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(400, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>As the pack's Sram: 50 sets on a cross of 1 a trap of no duration casting 51, 20 fire to the enemies in a cross of 1; 52 pushes an enemy 3 cells; 53 deals 10 fire from 4 cells exactly.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "C", "a,A"];
        var point = Zone(SpellZoneShape.Point);
        var cross = Zone(SpellZoneShape.Cross, 1);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1050, SpellId = SetTrap, Grade = 1, ApCost = 3, MinRange = 0, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1051, SpellId = TrapStrike, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1052, SpellId = Push, Grade = 1, ApCost = 2, MinRange = 1, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1053, SpellId = Shot, Grade = 1, ApCost = 3, MinRange = 4, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 400, TargetMask = 4, Triggers = 2, DiceNum = TrapStrike, DiceSide = 1, Zone = cross },
            new SpellEffect { Uid = 2, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 20, DiceSide = 20, Zone = cross },
            new SpellEffect { Uid = 3, EffectId = 5, TargetMask = 0, Triggers = 2, DiceNum = 3, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(params Combatant[] others)
    {
        return new ImaginedFight { Map = Open(), Fighters = [Caster(Off(0, 0)) with { ActionPoints = 12 }, .. others], ActorId = 1, Round = 1 };
    }

    [Fact]
    public void ATrapStaysUntilItIsTriggered()
    {
        var trapped = _tactics.Imagination.Cast(Fight(Enemy(-1, Off(6, 0))), Level(_spells, SetTrap), Off(3, 0));

        var later = _tactics.Imagination.TurnStarted(trapped, 1, 9);

        var trap = Assert.Single(later.Marks);
        Assert.Equal((CellMarkKind.Trap, 5), (trap.Kind, trap.Cells.Length));
    }

    [Fact]
    public void AFighterWalkingOntoATrapSetsItOffAndItStrikesTheEnemiesInItsZone()
    {
        var walker = Enemy(-1, Off(6, 0)) with { Life = 100, MovementPoints = 3 };
        var beside = Enemy(-2, Off(3, 1)) with { Life = 100 };
        var trapped = _tactics.Imagination.Cast(Fight(walker, beside), Level(_spells, SetTrap), Off(3, 0));

        var walked = _tactics.Imagination.Walked(trapped with { ActorId = walker.Id }, Off(4, 0), (0, 6));

        // Both a step from the trap's middle, its zone taking a tenth off as it says.
        Assert.Equal((82, 82), (walked.Find(walker.Id)!.Life, walked.Find(beside.Id)!.Life));
        Assert.Empty(walked.Marks);
    }

    [Fact]
    public void AWalkStopsOnTheFirstTrapCellItStepsOn()
    {
        var walker = Caster(Off(0, 0)) with { MovementPoints = 3 };
        var around = new int[4];
        var count = FightMoves.Steps(walker.Cell, around);
        var stops = CellSet.Of(around.AsSpan(0, count));

        var reached = new FightMoves(States()).Reachable(Open(), [walker], walker, stops);

        Assert.Equal(1 + count, reached.Count);
    }

    [Fact]
    public void APushStopsOnATrapsFirstCellAndSetsItOffWithNoPushDamage()
    {
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100 };
        var trapped = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, SetTrap), Off(4, 0));

        var pushed = _tactics.Imagination.Cast(trapped, Level(_spells, Push), Off(1, 0));

        Assert.Equal((Off(3, 0), 82), (pushed.Find(enemy.Id)!.Cell, pushed.Find(enemy.Id)!.Life));
        Assert.Empty(pushed.Marks);
    }

    [Fact]
    public void ATrapSetAroundAFighterStrikesNoOneUntilOneComes()
    {
        var enemy = Enemy(-1, Off(3, 1)) with { Life = 100 };

        var trapped = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, SetTrap), Off(3, 0));

        Assert.Equal(100, trapped.Find(enemy.Id)!.Life);
        Assert.Single(trapped.Marks);
    }

    [Fact]
    public void AnEnemyOfTheReplyWalkingOntoATrapSetsItOffBeforeItStrikes()
    {
        // From 2 MP away, only the trap's arm is 4 cells from the character, where the enemy's spell is cast from.
        var enemy = Enemy(-1, Off(6, 0)) with { Life = 100, MovementPoints = 2, ActionPoints = 6 };
        var start = Fight(enemy);
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, SetTrap)]).Add(enemy.Id, [Level(_spells, Shot)]) };
        var judge = _tactics.JudgeOf(start, knowledge, TacticWeights.Default);
        var trapped = _tactics.Imagination.Cast(start, Level(_spells, SetTrap), Off(3, 0));

        var replied = _tactics.Replies(trapped, knowledge, judge);

        Assert.Equal((Off(4, 0), 82), (replied.Find(enemy.Id)!.Cell, replied.Find(enemy.Id)!.Life));
        Assert.Empty(replied.Marks);
    }
}
