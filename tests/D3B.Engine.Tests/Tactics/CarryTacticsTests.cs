using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A Pandawa's carrying as the pack of 3.7.14.18 writes it, Karcham and a throw, and what the AI makes of it (D474).</summary>
public sealed class CarryTacticsTests
{
    private const int Karcham = 10;
    private const int Throw = 20;
    private const int Strike = 40;

    // As the pack of 3.7.14.18 writes them: Enraciné (6), not pushed nor swapped, its state effects 0, 1, 2, 17 and 18, here 0
    // and 18; Pesanteur (7), not swapped, effect 18; Lourd (63), effect 4 alone; Emprisonné (252), which cannot be moved,
    // effects 3 and 17; Assourdi (1349), not pushed, effect 0.
    private const int Rooted = 6;
    private const int Gravity = 7;
    private const int Heavy = 63;
    private const int Imprisoned = 252;
    private const int Deafened = 1349;

    private static readonly int[] _stateEffects = [0, 18, 18, 4, 3, 17, 0];
    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, CarryStates(), new EffectCatalog(new Effect[]
    {
        new(50, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(51, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(281, 0, 0, '\0', 3, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2999, 0, 0, '\0', 2, 4, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// As the pack of 3.7.14.18 writes them: Karcham (10), 1 AP at a range of 1 in line on a taken cell, 4 times a turn and once
    /// a target, cast sober and not carried or carrying; it carries an ally but the caster or an enemy while its caster carries
    /// nothing (50), raises its own range by 3 for the client to show (281), and throws while its caster carries (51). A throw
    /// (20), 2 AP at 1 to 4 on a free cell, cast carrying: it throws, then deals 30 earth to an enemy thrown and heals an ally
    /// thrown by 30, on the cell it lands on. A blow (40), 3 AP at a range of 1: 40 earth to an enemy.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["g,A,*e3", "C", "g,A,*E3", "a,A", "A,K", "g,K", "A", "I", "HS=3|HS!8", "HS=3"];
        var point = Zone(SpellZoneShape.Point);

        SpellLevel Level(int id, int spell, int cost, int least, int most, int first, int count, SpellLevelProperties properties, int criterion = SpellLevel.NoCriterion, int perTurn = 0, int perTarget = 0)
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
                MaxCastPerTarget = (byte)perTarget,
                StatesCriterion = criterion,
                Properties = properties,
                FirstEffect = first,
                EffectCount = (ushort)count,
            };
        }

        SpellEffect Effect(int effectId, int mask, int dice = 0, int side = 0, int value = 0, int duration = 0, SpellEffectProperties properties = SpellEffectProperties.None)
        {
            return new SpellEffect { EffectId = (ushort)effectId, TargetMask = mask, Triggers = 7, DiceNum = dice, DiceSide = side, Value = value, Duration = (sbyte)duration, Properties = properties, Zone = point };
        }

        SpellLevel[] levels =
        [
            Level(1010, Karcham, 1, 1, 1, 0, 3, SpellLevelProperties.CastInLine | SpellLevelProperties.NeedTakenCell, criterion: 8, perTurn: 4, perTarget: 1),
            Level(1020, Throw, 2, 1, 4, 3, 3, SpellLevelProperties.NeedFreeCell, criterion: 9, perTurn: 1),
            Level(1040, Strike, 3, 1, 1, 6, 1, SpellLevelProperties.None),
        ];
        SpellEffect[] effects =
        [
            Effect(50, 0),
            Effect(281, 1, dice: Karcham, value: 3, duration: -1, properties: SpellEffectProperties.ForClientOnly),
            Effect(51, 2),
            Effect(51, 3),
            Effect(97, 4, dice: 30, side: 30),
            Effect(2999, 5, dice: 30, side: 30),
            Effect(97, 6, dice: 40, side: 40),
        ];
        Spell[] spells =
        [
            new Spell { Id = Karcham, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Throw, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Strike, NameId = 4, FirstLevel = 2, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static SpellStateCatalog CarryStates()
    {
        return new SpellStateCatalog(
            new SpellState[]
            {
                new(Carrying.CarrierState, 1, SpellStateProperties.PreventsSpellCast | SpellStateProperties.PreventsFight, 0, 0, 0),
                new(Rooted, 4, SpellStateProperties.CantBePushed | SpellStateProperties.CantSwitchPosition, 0, 2, 0),
                new(Gravity, 5, SpellStateProperties.CantSwitchPosition, 2, 1, 0),
                new(Carrying.CarriedState, 2, SpellStateProperties.None, 3, 0, 0),
                new(Heavy, 6, SpellStateProperties.None, 3, 1, 0),
                new(Imprisoned, 7, SpellStateProperties.CantBeMoved, 4, 2, 0),
                new(Deafened, 8, SpellStateProperties.CantBePushed, 6, 1, 0),
                new(Carrying.HalvedState, 3, SpellStateProperties.None, 7, 0, 0),
            },
            _stateEffects);
    }

    private static Combatant Pandawa(int actionPoints)
    {
        return Caster() with { ActionPoints = actionPoints, MovementPoints = 0, Life = 500, MaxLife = 500, BreedId = 12 };
    }

    private static Combatant Ally(long id, int cell)
    {
        return Caster(cell) with { Id = id, Life = 300, MaxLife = 300, BreedId = 1 };
    }

    private static Combatant Target(long id, int cell)
    {
        return Enemy(id, cell) with { Life = 300, MaxLife = 300, MonsterProperties = MonsterProperties.CanTackle | MonsterProperties.CanMove | MonsterProperties.CanBePushed | MonsterProperties.CanBeCarried };
    }

    private static ImaginedFight Fight(params Combatant[] fighters)
    {
        return new ImaginedFight { Map = Open(), Fighters = [.. fighters], ActorId = fighters[0].Id, Round = 1 };
    }

    private static ImaginedFight WhileCarrying(Combatant pandawa, Combatant carried, params Combatant[] others)
    {
        return Fight([pandawa with { States = [Carrying.CarrierState] }, carried with { Cell = Combatant.NoCell, CarrierId = pandawa.Id, States = [Carrying.CarriedState, Carrying.HalvedState] }, .. others]);
    }

    private static FightKnowledge Knowledge(ImaginedFight fight, params int[] spells)
    {
        var known = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(fight.ActorId, [.. spells.Select(spell => Level(_spells, spell))]);

        return new FightKnowledge { Spells = known };
    }

    [Fact]
    public void ACarryTakesTheCarriedOffItsCellAndPutsBothInTheStatesTheServerSets()
    {
        // Measured on the night's capture of 2026-11-20 (D474): states 8 and 3579 on the carried, 3 on its carrier.
        var pandawa = Pandawa(6);
        var ally = Ally(2, Off(1, 0));

        var carried = _tactics.Imagination.Cast(Fight(pandawa, ally), Level(_spells, Karcham), ally.Cell);

        Assert.Equal(Combatant.NoCell, carried.Find(ally.Id)!.Cell);
        Assert.Equal(pandawa.Id, carried.Find(ally.Id)!.CarrierId);
        Assert.Equal([Carrying.CarriedState, Carrying.HalvedState], carried.Find(ally.Id)!.States);
        Assert.Equal([Carrying.CarrierState], carried.Actor.States);
        Assert.Equal(5, carried.Actor.ActionPoints);
    }

    [Theory]
    [InlineData(Imprisoned, false)]
    [InlineData(Heavy, false)]
    [InlineData(Rooted, false)]
    [InlineData(Gravity, false)]
    [InlineData(Deafened, true)]
    public void AFighterIsCarriedUnlessOneOfItsStatesKeepsItFromBeingMovedOrCarried(int state, bool carried)
    {
        // The references read a state's effects 3 and 4 so (D482), and the owner said Enraciné and Pesanteur keep a fighter from
        // being carried too: a carry takes no fighter in such a state, and still takes one a state only keeps from being pushed.
        var pandawa = Pandawa(6);
        var ally = Ally(2, Off(1, 0)) with { States = [state] };

        var cast = _tactics.Imagination.Cast(Fight(pandawa, ally), Level(_spells, Karcham), ally.Cell);

        Assert.Equal(carried, cast.Find(ally.Id)!.CarrierId == pandawa.Id);
        Assert.Equal(carried ? Combatant.NoCell : ally.Cell, cast.Find(ally.Id)!.Cell);
    }

    [Theory]
    [InlineData(4, 0, CastRefusal.None)]
    [InlineData(5, 0, CastRefusal.OutOfRange)]
    [InlineData(2, 0, CastRefusal.CellNotFree)]
    public void KarchamCastCarryingThrowsOntoAFreeCellAsFarAsItsOwnRangeBonusTakesIt(int dx, int dy, CastRefusal refusal)
    {
        // Measured on 3.7.12.12 (D191): the server's buffs gave the carrier's Karcham 3 more range, no taken cell and a free one.
        var fight = WhileCarrying(Pandawa(6), Ally(2, Off(1, 0)), Target(-1, Off(2, 0)));

        Assert.Equal(refusal, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Karcham), Off(dx, dy), fight.History, fight.Round));
    }

    [Fact]
    public void KarchamCastCarryingNothingNeedsAFighterBesideItsCaster()
    {
        var fight = Fight(Pandawa(6), Ally(2, Off(1, 0)));

        Assert.Equal(CastRefusal.CellNotTaken, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Karcham), Off(0, 1), fight.History, fight.Round));
        Assert.Equal(CastRefusal.None, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Karcham), Off(1, 0), fight.History, fight.Round));
    }

    [Fact]
    public void AThrowLandsTheCarriedOnTheCellCastOnAndEndsTheCarry()
    {
        var ally = Ally(2, Off(1, 0));
        var fight = WhileCarrying(Pandawa(6), ally);

        var thrown = _tactics.Imagination.Cast(fight, Level(_spells, Karcham), Off(4, 0));

        Assert.Equal(Off(4, 0), thrown.Find(ally.Id)!.Cell);
        Assert.Null(thrown.Find(ally.Id)!.CarrierId);
        Assert.Empty(thrown.Find(ally.Id)!.States);
        Assert.Empty(thrown.Actor.States);
        Assert.Equal(5, thrown.Actor.ActionPoints);
    }

    [Fact]
    public void AThrowStrikesAnEnemyItThrowsAndHealsAnAllyItThrows()
    {
        // The pack's throws hold an effect of mask A,K and one of g,K on the cell they land on: K names the fighter thrown, as
        // the references read it (D474).
        var enemy = Target(-1, Off(1, 0));
        var ally = Ally(2, Off(1, 0)) with { Life = 200 };

        var struck = _tactics.Imagination.Cast(WhileCarrying(Pandawa(6), enemy), Level(_spells, Throw), Off(3, 0));
        var healed = _tactics.Imagination.Cast(WhileCarrying(Pandawa(6), ally), Level(_spells, Throw), Off(3, 0));

        Assert.Equal(270, struck.Find(enemy.Id)!.Life);
        Assert.Equal(Off(3, 0), struck.Find(enemy.Id)!.Cell);
        Assert.Equal(230, healed.Find(ally.Id)!.Life);
    }

    [Fact]
    public void ACharacterThatBeganItsTurnCarryingThrowsBeforeItEndsIt()
    {
        // Seen on the night's capture of 2026-11-20 (D474): the Pandawa carried an ally, then stayed idle two turns.
        var ally = Ally(2, Off(1, 0));
        var fight = WhileCarrying(Pandawa(6), ally, Target(-1, Off(6, 0)));

        var plan = _tactics.Plan(fight, Knowledge(fight, Karcham, Throw), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Contains(plan.Actions, action => action.SpellId is Karcham or Throw);
    }

    [Fact]
    public void AnEnemyBesideTheCarrierIsCarriedThenThrownToBeStruck()
    {
        var enemy = Target(-1, Off(1, 0));
        var fight = Fight(Pandawa(3), enemy);

        var plan = _tactics.Plan(fight, Knowledge(fight, Karcham, Throw), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Karcham, Throw], plan.Actions.Where(action => action.Kind is TacticActionKind.Cast).Select(action => action.SpellId));
    }

    [Fact]
    public void ACarrierDoesNotEndItsTurnCarryingAnEnemyItCannotThrow()
    {
        // A carry it cannot throw again this turn leaves it carrying through the enemies' reply, which nothing measured (D474).
        var enemy = Target(-1, Off(1, 0));
        var fight = Fight(Pandawa(1), enemy);

        var plan = _tactics.Plan(fight, Knowledge(fight, Karcham, Throw), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.DoesNotContain(plan.Actions, action => action.SpellId is Karcham);
    }

    [Fact]
    public void AnAllyAboutToFallBesideAnEnemyIsCarriedAndThrownOutOfItsReach()
    {
        var ally = Ally(2, Off(1, 0)) with { Life = 60 };
        var enemy = Target(-1, Off(2, 0)) with { ActionPoints = 6, MovementPoints = 0, MonsterProperties = MonsterProperties.CanBeCarried };
        var fight = Fight(Pandawa(2), ally, enemy);
        var knowledge = Knowledge(fight, Karcham).With(enemy.Id, [Level(_spells, Strike)]);

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
        var end = fight;

        foreach (var action in plan.Actions.Where(action => action.Kind is TacticActionKind.Cast))
            end = _tactics.Imagination.Cast(end, Level(_spells, action.SpellId), action.Cell);

        Assert.Equal(2, plan.Actions.Count(action => action.SpellId is Karcham));
        Assert.True(MapGeometry.Distance(end.Find(ally.Id)!.Cell, enemy.Cell) > 1);
    }

    [Fact]
    public void ACarrierTheServerGaveARangeModifierThrowsNoFartherThanIt()
    {
        // Measured on 3.7.12.12 (D191): the server's buffs gave the range; the level's own is then not added again.
        var fight = WhileCarrying(Pandawa(6) with { Modifiers = [new SpellModifier(SpellModifierKind.Range, Karcham, 3)] }, Ally(2, Off(1, 0)));

        Assert.Equal(CastRefusal.None, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Karcham), Off(4, 0), fight.History, fight.Round));
        Assert.Equal(CastRefusal.OutOfRange, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Karcham), Off(5, 0), fight.History, fight.Round));
    }

    [Fact]
    public void AMonsterThePackDoesNotLetBeCarriedStaysOnItsCell()
    {
        var enemy = Target(-1, Off(1, 0)) with { MonsterProperties = MonsterProperties.CanMove };

        var after = _tactics.Imagination.Cast(Fight(Pandawa(6), enemy), Level(_spells, Karcham), enemy.Cell);

        Assert.Equal(enemy.Cell, after.Find(enemy.Id)!.Cell);
        Assert.Empty(after.Actor.States);
    }
}
