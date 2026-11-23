using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>"Maîtrise des invocations", the spell every character has that switches the control of its summons, and what the AI makes of it (D500).</summary>
public sealed class SummonMasteryTacticsTests
{
    private const int Mastery = 10;
    private const int Control = 11;
    private const int Freedom = 15;
    private const int Double = 16;
    private const int DoublesPair = 17;
    private const int Strike = 40;

    private const int Mastered = 13;
    private const int Freed = 14;
    private const int SummonControl = 2027;
    private const int Sram = 4;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(120, 0, 1, '+', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(406, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(792, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(SummonControl, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 10, as the pack of 3.7.14.18 holds 18646, a spell every character has: on the caster's cell, twice a turn,
    /// putting the caster in state 13 for the whole fight when it is not in it, dispelling itself when it is, casting 11,
    /// which gives the summons their control, and giving an AP. Free here, where 18646 costs the AP it gives back: the beam
    /// room its free casts took (D255) needs a search too big for a test, so that this one would cast it for its AP. Spell
    /// 15, its pair, as 22312: the same with state 14. Spell 16, a breed's, as the Sram's double 12915: free, once a turn,
    /// casting 11 too, and giving an AP. Spell 40, 4 AP at range 1 to 4: an earth blow of 40.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["C", "I", "C,*e13", "C,*E13", "A", "C,*e14", "C,*E14"];
        var point = Zone(SpellZoneShape.Point);

        SpellLevel Level(int id, int spell, int cost, int least, int most, int first, int count, int perTurn = 0)
        {
            return new SpellLevel
            {
                Id = id,
                SpellId = spell,
                Grade = 1,
                ApCost = (short)cost,
                MinRange = (byte)least,
                Range = (byte)most,
                MaxCastPerTurn = (sbyte)perTurn,
                StatesCriterion = SpellLevel.NoCriterion,
                FirstEffect = first,
                EffectCount = (ushort)count,
            };
        }

        SpellEffect Effect(int effectId, int mask, int dice = 0, int side = 0, int value = 0, int duration = 0)
        {
            return new SpellEffect { EffectId = (ushort)effectId, TargetMask = mask, Triggers = 1, DiceNum = dice, DiceSide = side, Value = value, Duration = (sbyte)duration, Zone = point };
        }

        SpellLevel[] levels =
        [
            Level(1010, Mastery, 0, 0, 0, 0, 4, perTurn: 2),
            Level(1011, Control, 0, 0, 0, 4, 1),
            Level(1015, Freedom, 0, 0, 0, 5, 4, perTurn: 2),
            Level(1016, Double, 0, 0, 0, 9, 2, perTurn: 1),
            Level(1040, Strike, 4, 1, 4, 11, 1),
        ];
        SpellEffect[] effects =
        [
            Effect(950, 2, value: Mastered, duration: -1),
            Effect(792, 2, dice: Control, side: 1),
            Effect(406, 3, value: Mastery),
            Effect(120, 0, dice: 1),
            Effect(SummonControl, 0, duration: -1),
            Effect(950, 5, value: Freed, duration: -1),
            Effect(792, 5, dice: Control, side: 1),
            Effect(406, 6, value: Freedom),
            Effect(120, 0, dice: 1),
            Effect(792, 0, dice: Control, side: 1),
            Effect(120, 0, dice: 1),
            Effect(97, 4, dice: 40, side: 40),
        ];
        Spell[] spells =
        [
            new Spell { Id = Mastery, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Control, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Freedom, NameId = 3, FirstLevel = 2, LevelCount = 1 },
            new Spell { Id = Double, NameId = 4, FirstLevel = 3, LevelCount = 1 },
            new Spell { Id = Strike, NameId = 5, FirstLevel = 4, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            new[] { new SpellVariant(1, SpellVariant.CommonBreed, Mastery, Freedom), new SpellVariant(2, Sram, Double, DoublesPair) }, Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(Combatant character, Combatant enemy)
    {
        return new ImaginedFight { Map = Open(), Fighters = [character, enemy], ActorId = character.Id, Round = 1 };
    }

    private static FightKnowledge Knowledge(Combatant character, params int[] spells)
    {
        return new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(character.Id, [.. spells.Select(spell => Level(_spells, spell))]) };
    }

    private static Combatant Character(int actionPoints, params int[] states)
    {
        return Caster() with { ActionPoints = actionPoints, MovementPoints = 0, States = [.. states] };
    }

    private static Combatant Target()
    {
        return Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300 };
    }

    private static List<int> Played(ImaginedFight fight, params int[] spells)
    {
        // The bot plans again before each action and plays the first.
        var casts = new List<int>();

        for (var step = 0; step < 8; step++)
        {
            var plan = _tactics.Plan(fight, Knowledge(fight.Actor, spells), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

            if (plan.IsEmpty)
                break;

            var action = plan.Actions[0];

            casts.Add(action.SpellId);
            fight = _tactics.Imagination.Cast(fight, Level(_spells, action.SpellId), action.Cell);
        }

        return casts;
    }

    [Fact]
    public void TheMasteryCastTwiceLeavesTheCasterInTheStatesItHeld()
    {
        var character = Character(7);
        var fight = Fight(character, Target());

        var on = _tactics.Imagination.Cast(fight, Level(_spells, Mastery), character.Cell);
        var off = _tactics.Imagination.Cast(on, Level(_spells, Mastery), character.Cell);

        Assert.Equal([Mastered], on.Actor.States);
        Assert.Empty(off.Actor.States);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACharacterNeverCastsTheMasteryNorItsPair(bool mastered)
    {
        // The owner's report: the characters of a group, a Huppermage the most, cast "Maîtrise des invocations" turn after turn
        // for nothing. Here its AP would let a second blow come; the control of the summons is still the player's to set.
        var casts = Played(Fight(mastered ? Character(7, Mastered) : Character(7), Target()), Mastery, Freedom, Strike);

        Assert.DoesNotContain(Mastery, casts);
        Assert.DoesNotContain(Freedom, casts);
    }

    [Fact]
    public void ABreedsSpellThatGivesItsSummonsTheirControlIsStillCast()
    {
        var casts = Played(Fight(Character(3), Target()), Double, Strike);

        Assert.Equal([Double, Strike], casts);
    }

    [Fact]
    public void ACharacterWhoHoldsTheMasteryStillStrikes()
    {
        var casts = Played(Fight(Character(7, Mastered), Target()), Mastery, Strike);

        Assert.Equal([Strike], casts);
    }
}
