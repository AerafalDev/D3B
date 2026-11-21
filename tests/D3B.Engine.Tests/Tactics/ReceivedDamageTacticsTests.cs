using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The buffs on the damage a fighter receives as the AI imagines a cast giving them: 1163 multiplying it, 105 taking points off each blow.</summary>
public sealed class ReceivedDamageTacticsTests
{
    private const int Strike = 40;
    private const int Expose = 50;
    private const int Guard = 60;
    private const int Feint = 70;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(105, 0, 16, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1163, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 40, an earth blow of 40 at range 1 to 4. Spell 50, the target's damage received times 120% as a Iop's Bond gives it,
    /// waiting for the damage (trigger D). Spell 60, 4 points off each blow its caster receives, as the monsters' buff of 105 gives
    /// it. Spell 70, times 120% only on the damage of a distance (trigger DR), as Holmgang's condition.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "C", "I", "D", "DR"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1000 + Strike, SpellId = Strike, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1000 + Expose, SpellId = Expose, Grade = 1, ApCost = 2, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1000 + Guard, SpellId = Guard, Grade = 1, ApCost = 2, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1000 + Feint, SpellId = Feint, Grade = 1, ApCost = 2, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 2, DiceNum = 40, DiceSide = 40, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 1163, TargetMask = 0, Triggers = 3, DiceNum = 120, Duration = 1, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 105, TargetMask = 1, Triggers = 3, Value = 4, Duration = 1, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 1163, TargetMask = 0, Triggers = 4, DiceNum = 120, Duration = 1, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight()
    {
        return new ImaginedFight { Map = Open(), Fighters = [Caster() with { ActionPoints = 10 }, Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300, ActionPoints = 10 }], ActorId = 1, Round = 1 };
    }

    private static int LifeAfter(ImaginedFight fight, long id)
    {
        return fight.Fighters.First(fighter => fighter.Id == id).Life;
    }

    [Fact]
    public void ABlowAfterACastMultiplyingTheDamageItsTargetReceivesStrikesHarder()
    {
        var exposed = _tactics.Imagination.Cast(Fight(), Level(_spells, Expose), Off(2, 0));
        var struck = _tactics.Imagination.Cast(exposed, Level(_spells, Strike), Off(2, 0));

        Assert.Equal(300 - 48, LifeAfter(struck, -1));
    }

    [Fact]
    public void AMultiplierOnlyOnSomeDamageIsNotForeseen()
    {
        var feinted = _tactics.Imagination.Cast(Fight(), Level(_spells, Feint), Off(2, 0));
        var struck = _tactics.Imagination.Cast(feinted, Level(_spells, Strike), Off(2, 0));

        Assert.Equal(300 - 40, LifeAfter(struck, -1));
    }

    [Fact]
    public void ABlowOnAFighterThatGuardedItselfLosesTheGuardsPoints()
    {
        var guarded = _tactics.Imagination.Cast(Fight() with { ActorId = -1 }, Level(_spells, Guard), Off(2, 0));
        var struck = _tactics.Imagination.Cast(guarded with { ActorId = 1 }, Level(_spells, Strike), Off(2, 0));

        Assert.Equal(300 - 36, LifeAfter(struck, -1));
    }
}
