using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A Huppermage's runes as the AI imagines them, as the pack of 3.7.14.18 sets and triggers them (D493).</summary>
public sealed class RuneTacticsTests
{
    private const int Flame = 30;
    private const int FireRune = 31;
    private const int RuneStrike = 32;
    private const int Runification = 33;
    private const int Trigger = 34;
    private const int Overload = 35;
    private const int Gust = 36;
    private const int FireState = 37;
    private const int AirState = 38;
    private const int Combination = 39;
    private const int Blend = 40;
    private const int RuneState = 296;
    private const int Fire = 290;
    private const int Air = 293;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(98, 0, 0, '\0', 0, 4, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 0, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(951, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1160, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2022, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2023, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2794, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// As the pack's Huppermage: 30 deals 10 fire to an enemy and casts 31 on it, which sets on its cell, for 2 rounds, a rune
    /// casting 32, 20 fire to an enemy there while its caster holds the rune state; 33 gives its caster that state, casts 34 on
    /// the cell, which triggers the runes there, and takes the state back; 35 does so with a trigger over the whole map. 36
    /// deals 5 air and casts 38 on the enemy, 30 casting 37 too: each gives its element's state to an enemy without it and
    /// casts 39, which casts 40, 30 fire, on an enemy holding both.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "C", "a,A", "A,*E296", "A,e290", "A,e293", "A,E290,E293"];
        var point = Zone(SpellZoneShape.Point);
        var map = Zone(SpellZoneShape.WholeMap);
        SpellLevel[] levels =
        [
            Graded(Flame, 3, 0, 3),
            Graded(FireRune, 0, 3, 1),
            Graded(RuneStrike, 0, 4, 1),
            Graded(Runification, 2, 5, 3),
            Graded(Trigger, 0, 8, 1),
            Graded(Overload, 2, 9, 3),
            Graded(Gust, 3, 12, 2),
            Graded(FireState, 0, 14, 2),
            Graded(AirState, 0, 16, 2),
            Graded(Combination, 0, 18, 1),
            Graded(Blend, 0, 19, 1),
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 1160, TargetMask = 0, Triggers = 2, DiceNum = FireRune, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 1160, TargetMask = 0, Triggers = 2, DiceNum = FireState, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 2022, TargetMask = 4, Triggers = 2, Duration = 2, DiceNum = RuneStrike, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 5, EffectId = 99, TargetMask = 5, Triggers = 2, DiceNum = 20, DiceSide = 20, Zone = point },
            new SpellEffect { Uid = 6, EffectId = 950, TargetMask = 3, Triggers = 2, Duration = 1, Value = RuneState, Zone = point },
            new SpellEffect { Uid = 7, EffectId = 2794, TargetMask = 3, Triggers = 2, DiceNum = Trigger, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 8, EffectId = 951, TargetMask = 3, Triggers = 2, Value = RuneState, Zone = point },
            new SpellEffect { Uid = 9, EffectId = 2023, TargetMask = 4, Triggers = 2, Zone = point },
            new SpellEffect { Uid = 10, EffectId = 950, TargetMask = 3, Triggers = 2, Duration = 1, Value = RuneState, Zone = point },
            new SpellEffect { Uid = 11, EffectId = 2023, TargetMask = 4, Triggers = 2, Zone = map },
            new SpellEffect { Uid = 12, EffectId = 951, TargetMask = 3, Triggers = 2, Value = RuneState, Zone = point },
            new SpellEffect { Uid = 13, EffectId = 98, TargetMask = 0, Triggers = 2, DiceNum = 5, DiceSide = 5, Zone = point },
            new SpellEffect { Uid = 14, EffectId = 1160, TargetMask = 0, Triggers = 2, DiceNum = AirState, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 15, EffectId = 950, TargetMask = 6, Triggers = 2, Duration = -1, Value = Fire, Zone = point },
            new SpellEffect { Uid = 16, EffectId = 1160, TargetMask = 0, Triggers = 2, DiceNum = Combination, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 17, EffectId = 950, TargetMask = 7, Triggers = 2, Duration = -1, Value = Air, Zone = point },
            new SpellEffect { Uid = 18, EffectId = 1160, TargetMask = 0, Triggers = 2, DiceNum = Combination, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 19, EffectId = 1160, TargetMask = 8, Triggers = 2, DiceNum = Blend, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 20, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 30, DiceSide = 30, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static SpellLevel Graded(int spell, int cost, int first, int count)
    {
        return new SpellLevel { Id = 1000 + spell, SpellId = spell, Grade = 1, ApCost = (short)cost, MinRange = 0, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = first, EffectCount = (ushort)count };
    }

    private static ImaginedFight Fight(params Combatant[] others)
    {
        return new ImaginedFight { Map = Open(), Fighters = [Caster(Off(0, 0)) with { ActionPoints = 12 }, .. others], ActorId = 1, Round = 1 };
    }

    [Fact]
    public void AnElementalSpellSetsTheRuneItsCastSpellSetsOnTheEnemysCell()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Flame), Off(3, 0));

        var rune = Assert.Single(after.Marks);
        Assert.Equal((CellMarkKind.Rune, RuneStrike, Off(3, 0), 3, 1L), (rune.Kind, rune.SpellId, rune.Centre, rune.EndRound, rune.AuthorId));
        Assert.Equal(90, after.Find(enemy.Id)!.Life);
    }

    [Fact]
    public void ARuneStrikesNoOneUntilItsAuthorTriggersIt()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var runed = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Flame), Off(3, 0));

        var started = _tactics.Imagination.TurnStarted(runed, enemy.Id, 2);

        Assert.Equal(90, started.Find(enemy.Id)!.Life);
        Assert.Single(started.Marks);
    }

    [Fact]
    public void RunificationOnARuneStrikesTheFighterOnItWithTheRunesSpellThenTheRuneGoes()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var runed = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Flame), Off(3, 0));

        var triggered = _tactics.Imagination.Cast(runed, Level(_spells, Runification), Off(3, 0));

        Assert.Equal(70, triggered.Find(enemy.Id)!.Life);
        Assert.Empty(triggered.Marks);
        Assert.False(triggered.Find(1)!.HasState(RuneState));
    }

    [Fact]
    public void RunificationOnACellWithNoRuneTriggersNone()
    {
        var first = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var second = Enemy(-2, Off(5, 0)) with { Life = 100 };
        var runed = _tactics.Imagination.Cast(Fight(first, second), Level(_spells, Flame), Off(3, 0));

        var triggered = _tactics.Imagination.Cast(runed, Level(_spells, Runification), Off(5, 0));

        Assert.Equal((90, 100), (triggered.Find(first.Id)!.Life, triggered.Find(second.Id)!.Life));
        Assert.Single(triggered.Marks);
    }

    [Fact]
    public void ATriggerOverTheWholeMapTriggersEveryRuneOfItsCasterAndNoOneElses()
    {
        var first = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var second = Enemy(-2, Off(5, 0)) with { Life = 100 };
        var runed = _tactics.Imagination.Cast(Fight(first, second), Level(_spells, Flame), Off(3, 0));
        var foreign = new CellMark(-200, 7, 0, CellMarkKind.Rune, RuneStrike, 1, Off(5, 0), [Off(5, 0)], 3);

        var triggered = _tactics.Imagination.Cast(runed with { Marks = runed.Marks.Add(foreign) }, Level(_spells, Overload), Off(0, 0));

        Assert.Equal((70, 100), (triggered.Find(first.Id)!.Life, triggered.Find(second.Id)!.Life));
        Assert.Equal([foreign], triggered.Marks);
    }

    [Fact]
    public void ARuneSetWhereOneIsTakesItsPlace()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var once = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Flame), Off(3, 0));

        var twice = _tactics.Imagination.Cast(once with { Round = 2 }, Level(_spells, Flame), Off(3, 0));

        Assert.Equal(4, Assert.Single(twice.Marks).EndRound);
    }

    [Fact]
    public void TwoElementsOnAnEnemyCombineAsTheirStatesSpellsSay()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var fired = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Flame), Off(3, 0));

        var blown = _tactics.Imagination.Cast(fired, Level(_spells, Gust), Off(3, 0));

        Assert.True(fired.Find(enemy.Id)!.HasState(Fire));
        Assert.Equal(90 - 5 - 30, blown.Find(enemy.Id)!.Life);
    }

    [Fact]
    public void TheServersRuneIsKnownByTheSpellThatSetIt()
    {
        var glyphs = new Glyphs(_spells);

        Assert.Equal(CellMarkKind.Rune, glyphs.KindOf(FireRune, RuneStrike));
    }
}
