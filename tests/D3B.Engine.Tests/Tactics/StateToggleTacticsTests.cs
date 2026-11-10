using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A spell that swaps two states, as a Pandawa's Bombance and Picole swap Sobre and Saoul, and what the AI makes of casting it (D420).</summary>
public sealed class StateToggleTacticsTests
{
    private const int Toggle = 10;
    private const int Drink = 20;
    private const int Sober = 30;
    private const int Strike = 40;

    private const int SoberState = 7;
    private const int DrunkState = 8;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(792, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(951, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 10, 1 AP on the caster's cell, up to 6 times a turn: casts 20, taking Sobre (7) off and putting Saoul (8) on for 2
    /// rounds, when the caster is sober, and 30, the other way round with Sobre for the whole fight, when it is drunk. Spell 40,
    /// 3 AP at range 1 to 4: an earth blow of 40 which the enemy takes only when its caster is sober.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["C", "I", "C,E7", "C,E8", "A,*E7"];
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
            Level(1010, Toggle, 1, 0, 0, 0, 2, perTurn: 6),
            Level(1020, Drink, 0, 0, 0, 2, 2),
            Level(1030, Sober, 0, 0, 0, 4, 2),
            Level(1040, Strike, 3, 1, 4, 6, 1),
        ];
        SpellEffect[] effects =
        [
            Effect(792, 2, dice: Drink, side: 1),
            Effect(792, 3, dice: Sober, side: 1),
            Effect(951, 0, value: SoberState),
            Effect(950, 0, value: DrunkState, duration: 2),
            Effect(951, 0, value: DrunkState),
            Effect(950, 0, value: SoberState, duration: -1),
            Effect(97, 4, dice: 40, side: 40),
        ];
        Spell[] spells =
        [
            new Spell { Id = Toggle, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Drink, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Sober, NameId = 3, FirstLevel = 2, LevelCount = 1 },
            new Spell { Id = Strike, NameId = 4, FirstLevel = 3, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(Combatant pandawa, Combatant enemy)
    {
        return new ImaginedFight { Map = Open(), Fighters = [pandawa, enemy], ActorId = pandawa.Id, Round = 1 };
    }

    private static FightKnowledge Knowledge(Combatant pandawa)
    {
        return new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(pandawa.Id, [Level(_spells, Toggle), Level(_spells, Strike)]) };
    }

    private static Combatant Pandawa(int state, int actionPoints)
    {
        return Caster() with { ActionPoints = actionPoints, MovementPoints = 0, States = [state] };
    }

    private static Combatant Target()
    {
        return Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300 };
    }

    [Fact]
    public void ACastThatSwapsTheStatesBackHoldsTheOnesTheCasterStartedWith()
    {
        var pandawa = Pandawa(SoberState, 6);
        var fight = Fight(pandawa, Target());

        var drunk = _tactics.Imagination.Cast(fight, Level(_spells, Toggle), pandawa.Cell);
        var sober = _tactics.Imagination.Cast(drunk, Level(_spells, Toggle), pandawa.Cell);

        Assert.Equal([DrunkState], drunk.Actor.States);
        Assert.Equal([SoberState], sober.Actor.States);
    }

    [Fact]
    public void AStateTheCasterAlreadyHeldWhenTheTurnBeganIsNotWorthAnythingCastAgain()
    {
        // Reported by players: a Pandawa cast Picole turn after turn and almost never struck. Drunk, then sober by a cast, it
        // was credited the Sobre it had held all along for the rest of the fight, a gain no strike matched.
        var pandawa = Pandawa(SoberState, 6);
        var fight = Fight(pandawa, Target());
        var judge = _tactics.JudgeOf(fight, Knowledge(pandawa), TacticWeights.Default);

        var back = _tactics.Imagination.Cast(_tactics.Imagination.Cast(fight, Level(_spells, Toggle), pandawa.Cell), Level(_spells, Toggle), pandawa.Cell);

        Assert.True(judge.Judge(back) <= judge.Judge(fight));
    }

    [Fact]
    public void ASoberCharacterStrikesRatherThanSwappingItsStatesForNothing()
    {
        var pandawa = Pandawa(SoberState, 4);
        var plan = _tactics.Plan(Fight(pandawa, Target()), Knowledge(pandawa), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Strike], plan.Actions.Select(action => action.SpellId));
    }

    [Fact]
    public void ADrunkCharacterWhoCannotStrikeSwapsItsStatesToStrike()
    {
        var pandawa = Pandawa(DrunkState, 4);
        var plan = _tactics.Plan(Fight(pandawa, Target()), Knowledge(pandawa), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Toggle, Strike], plan.Actions.Select(action => action.SpellId));
    }

    [Theory]
    [InlineData(DrunkState, 1)]
    [InlineData(SoberState, 0)]
    public void ACharacterReplanningAfterEachCastStrikesAndSwapsItsStatesNoMoreThanItMust(int state, int swaps)
    {
        // The bot plans again before each action and plays the first: the AI must reach its blow, not turn about.
        var fight = Fight(Pandawa(state, 7), Target());
        var casts = new List<int>();

        for (var step = 0; step < 8; step++)
        {
            var plan = _tactics.Plan(fight, Knowledge(fight.Actor), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

            if (plan.IsEmpty)
                break;

            var action = plan.Actions[0];

            casts.Add(action.SpellId);
            fight = _tactics.Imagination.Cast(fight, Level(_spells, action.SpellId), action.Cell);
        }

        Assert.Equal(swaps, casts.Count(spell => spell is Toggle));
        Assert.Contains(Strike, casts);
    }
}
