using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A cast that moves its caster, towards its target (1042) or away from it (1041), as the AI imagines it.</summary>
public sealed class CasterMoveTacticsTests
{
    private const int Dash = 40;
    private const int Recoil = 50;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1041, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1042, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 40, as an Ecaflip's Toupet: 4 AP at range 1 to 6, the caster moved 4 cells towards its target, then an earth blow of
    /// 30. Spell 50, as a Crâ's Tir de Repli: 3 AP at range 1 to 3, an earth blow of 20, then the caster moved 3 cells away.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1000 + Dash, SpellId = Dash, Grade = 1, ApCost = 4, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 2 },
            new SpellLevel { Id = 1000 + Recoil, SpellId = Recoil, Grade = 1, ApCost = 3, MinRange = 1, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 2 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 1042, TargetMask = 0, Triggers = 1, DiceNum = 4, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 20, DiceSide = 20, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 1041, TargetMask = 0, Triggers = 1, DiceNum = 3, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(int enemy, params int[] blocked)
    {
        return new ImaginedFight { Map = Open(blocked), Fighters = [Caster(), Enemy(-1, enemy) with { Life = 300, MaxLife = 300 }], ActorId = 1, Round = 1 };
    }

    [Fact]
    public void ACastMovingItsCasterTowardsItsTargetStopsBesideIt()
    {
        var cast = _tactics.Imagination.Cast(Fight(Off(3, 0)), Level(_spells, Dash), Off(3, 0));

        Assert.Equal(Off(2, 0), cast.Actor.Cell);
        Assert.Equal(270, cast.Fighters.First(fighter => fighter.Id == -1).Life);
    }

    [Fact]
    public void ACastMovingItsCasterTowardsAFarTargetMovesItItsCells()
    {
        var cast = _tactics.Imagination.Cast(Fight(Off(6, 0)), Level(_spells, Dash), Off(6, 0));

        Assert.Equal(Off(4, 0), cast.Actor.Cell);
    }

    [Fact]
    public void ACastMovingItsCasterAwayFromItsTargetMovesItBackUntilACellIsTaken()
    {
        Assert.Equal(Off(-3, 0), _tactics.Imagination.Cast(Fight(Off(1, 0)), Level(_spells, Recoil), Off(1, 0)).Actor.Cell);
        Assert.Equal(Off(-1, 0), _tactics.Imagination.Cast(Fight(Off(1, 0), Off(-2, 0)), Level(_spells, Recoil), Off(1, 0)).Actor.Cell);
    }

    [Fact]
    public void ACasterMovedAgainstAnObstacleTakesNoDamage()
    {
        var cast = _tactics.Imagination.Cast(Fight(Off(1, 0), Off(-1, 0)), Level(_spells, Recoil), Off(1, 0));

        Assert.Equal(Middle, cast.Actor.Cell);
        Assert.Equal(100, cast.Actor.Life);
    }
}
