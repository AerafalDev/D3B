using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A share or a reduction of the damage received that a spell gives, taken back as that spell is dispelled, as a Pandawa's Saoul is by Sobre (D495).</summary>
public sealed class DamageShareDispelTests
{
    private const int Drink = 20;
    private const int Sober = 30;
    private const int Shield = 40;
    private const int Ward = 50;

    private const int DrunkState = 8;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(105, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(406, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1163, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 20, 1 AP on the caster: the state 8 and 85% of the damage received for 2 rounds, as Saoul gives. Spell 30, 1 AP on
    /// the caster: dispels spell 20, as Sobre does. Spell 40, 1 AP on the caster: a reduction of 10 for 2 rounds, then dispelled
    /// by spell 30 too. Spell 50, 1 AP on the caster, of 1 stack: 85% of the damage received for 2 rounds.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["C", "I"];
        var point = Zone(SpellZoneShape.Point);

        SpellLevel Level(int id, int spell, int first, int count, int stacks = 0)
        {
            return new SpellLevel { Id = id, SpellId = spell, Grade = 1, ApCost = 1, MaxStack = (sbyte)stacks, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = first, EffectCount = (ushort)count };
        }

        SpellEffect Effect(int effectId, int dice = 0, int value = 0, int duration = 0)
        {
            return new SpellEffect { EffectId = (ushort)effectId, TargetMask = 0, Triggers = 1, DiceNum = dice, Value = value, Duration = (sbyte)duration, Zone = point };
        }

        SpellLevel[] levels =
        [
            Level(1020, Drink, 0, 2),
            Level(1030, Sober, 2, 2),
            Level(1040, Shield, 4, 1),
            Level(1050, Ward, 5, 1, stacks: 1),
        ];
        SpellEffect[] effects =
        [
            Effect(950, value: DrunkState, duration: 2),
            Effect(1163, dice: 85, duration: 2),
            Effect(406, value: Drink),
            Effect(406, value: Shield),
            Effect(105, dice: 10, duration: 2),
            Effect(1163, dice: 85, duration: 2),
        ];
        Spell[] spells =
        [
            new Spell { Id = Drink, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Sober, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Shield, NameId = 3, FirstLevel = 2, LevelCount = 1 },
            new Spell { Id = Ward, NameId = 4, FirstLevel = 3, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(Combatant caster)
    {
        return new ImaginedFight { Map = Open(), Fighters = [caster, Enemy(-1, Off(3, 0))], ActorId = caster.Id, Round = 1 };
    }

    private static ImaginedFight Cast(ImaginedFight fight, int spell)
    {
        return _tactics.Imagination.Cast(fight, Level(_spells, spell), fight.Actor.Cell);
    }

    [Fact]
    public void ADispelTakesBackTheDamageShareItsSpellGave()
    {
        // Reported by the owner (D495): a Pandawa cast Bombance three times, drunk, sober, drunk, for the turn's end of one cast,
        // the AI keeping Saoul's share of the damage received once Sobre dispelled it and adding a second.
        var fight = Fight(Caster() with { ActionPoints = 6 });

        var drunk = Cast(fight, Drink);
        var sober = Cast(drunk, Sober);
        var again = Cast(sober, Drink);

        Assert.Equal([85], drunk.Actor.ReceivedDamageShares);
        Assert.Empty(sober.Actor.ReceivedDamageShares);
        Assert.Equal([85], again.Actor.ReceivedDamageShares);
    }

    [Fact]
    public void ADispelTakesBackTheReductionItsSpellGave()
    {
        var fight = Fight(Caster() with { ActionPoints = 6, Level = 40 });

        var shielded = Cast(fight, Shield);
        var dispelled = Cast(shielded, Sober);

        Assert.Equal(30, shielded.Actor.ReceivedDamageReduction);
        Assert.Equal(0, dispelled.Actor.ReceivedDamageReduction);
    }

    [Fact]
    public void ADispelTakesBackAShareTheServerGave()
    {
        // The share a fighter holds as the turn begins is the server's buff of 1163, held by the spell that gave it.
        var caster = Caster() with { ActionPoints = 6, ReceivedDamageShares = [85], Buffs = [new HeldBuff(Drink, 1163, 85)] };

        var sober = Cast(Fight(caster), Sober);

        Assert.Empty(sober.Actor.ReceivedDamageShares);
        Assert.Empty(sober.Actor.Buffs);
    }

    [Fact]
    public void AShareCastAgainPastItsStacksReplacesTheOldest()
    {
        // The review's case (D495): a spell of one stack cast twice gives its share once, as a boost past its stacks does (D227).
        var fight = Fight(Caster() with { ActionPoints = 6 });

        var twice = Cast(Cast(fight, Ward), Ward);

        Assert.Equal([85], twice.Actor.ReceivedDamageShares);
    }
}
