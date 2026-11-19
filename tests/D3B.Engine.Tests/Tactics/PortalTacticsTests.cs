using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>An Eliotrope's portals as the AI imagines them (D468): set by a cast, a cast on an open one coming out of the last of its chain.</summary>
public sealed class PortalTacticsTests
{
    private const int Portail = 30;
    private const int Blow = 31;
    private const int Twofold = 32;
    private const int Shove = 33;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(5, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 0, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1181, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 30 sets a portal on a free cell with none, as the pack's Portail, its effect 1181 of dice 2 and 44338; 31 deals 10
    /// fire to an enemy; 32 deals 10 fire to an enemy cast straight and 20 through a portal, as an Eliotrope's spells hold both;
    /// 33 pushes an enemy 2 cells. Each is cast at a range of 1 to 6.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "A,r", "A,R", "a,A"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1030, SpellId = Portail, Grade = 1, ApCost = 1, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, Properties = SpellLevelProperties.NeedFreeCell | SpellLevelProperties.NeedCellWithoutPortal, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1031, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1032, SpellId = Twofold, Grade = 1, ApCost = 3, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 2 },
            new SpellLevel { Id = 1033, SpellId = Shove, Grade = 1, ApCost = 2, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 4, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 1181, TargetMask = 5, Triggers = 2, Duration = -1, DiceNum = 2, DiceSide = 44338, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 99, TargetMask = 3, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 99, TargetMask = 4, Triggers = 2, DiceNum = 20, DiceSide = 20, Zone = point },
            new SpellEffect { Uid = 5, EffectId = 5, TargetMask = 0, Triggers = 2, DiceNum = 2, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static CellMark Portal(int id, int cell, int team = 0)
    {
        return new CellMark(id, 1, team, CellMarkKind.Portal, 2, 44338, cell, [cell], 1_001);
    }

    private static ImaginedFight Fight(ImmutableArray<CellMark> marks, params Combatant[] others)
    {
        return new ImaginedFight { Map = Open(), Fighters = [Caster(Off(0, 0)), .. others], ActorId = 1, Round = 1, Marks = marks };
    }

    [Fact]
    public void APortailCastSetsAPortalOnTheCellKeptTheWholeFight()
    {
        var after = _tactics.Imagination.Cast(Fight([]), Level(_spells, Portail), Off(3, 0));

        var portal = Assert.Single(after.Marks);
        Assert.Equal((CellMarkKind.Portal, Off(3, 0), 0, 2), (portal.Kind, portal.Centre, portal.Team, portal.SpellId));
    }

    [Fact]
    public void APortalTheServerSetWithNoEndStaysAsItsAuthorsNextTurnBegins()
    {
        // The server names no end for a portal (client-game.md), which the fight's state reads as round 0.
        var fromServer = new CellMark(7, 1, 0, CellMarkKind.Portal, 2, 44338, Off(2, 0), [Off(2, 0)], 0);

        var started = _tactics.Imagination.TurnStarted(Fight([fromServer]), 1, 3);

        Assert.Equal([7], started.Marks.Select(mark => mark.Id));
    }

    [Fact]
    public void AFifthPortalOfATeamTakesThePlaceOfItsOldest()
    {
        ImmutableArray<CellMark> four = [Portal(1, Off(1, 1)), Portal(2, Off(2, 1)), Portal(3, Off(3, 1)), Portal(4, Off(4, 1))];

        var after = _tactics.Imagination.Cast(Fight(four), Level(_spells, Portail), Off(3, 0));

        Assert.Equal([2, 3, 4], after.Marks.Take(3).Select(mark => mark.Id));
        Assert.Equal(Off(3, 0), after.Marks[^1].Centre);
    }

    [Fact]
    public void ACastOnAnOpenPortalStrikesWhereItComesOutItsDamageRaisedTwoPercentACellBetweenThePortals()
    {
        var there = Enemy(-1, Off(4, 8)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight([Portal(1, Off(2, 0)), Portal(2, Off(2, 8))], there), Level(_spells, Blow), Off(2, 0));

        // 8 cells between the portals: 16 percent more of 10, rounded down.
        Assert.Equal(89, after.Find(there.Id)!.Life);
    }

    [Fact]
    public void AnEffectAskingAPortalLandsThroughOneAndOneAskingNoneOnAStraightCastOnly()
    {
        var beside = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var there = Enemy(-2, Off(4, 8)) with { Life = 100 };
        var fight = Fight([Portal(1, Off(2, 0)), Portal(2, Off(2, 8))], beside, there);

        Assert.Equal(90, _tactics.Imagination.Cast(fight, Level(_spells, Twofold), Off(3, 0)).Find(beside.Id)!.Life);
        Assert.Equal(77, _tactics.Imagination.Cast(fight, Level(_spells, Twofold), Off(2, 0)).Find(there.Id)!.Life);
    }

    [Fact]
    public void AnAllyStrikesThroughAnEliotropesPortalsAnEnemyItsBlowCannotReachStraight()
    {
        // The owner's ask (D468): every character of the bot casts through an ally's portals when it is worth it.
        var iop = Caster(Off(0, 0)) with { BreedId = 8, MovementPoints = 0 };
        var eliotrope = Caster(Off(-1, 0)) with { Id = 2, BreedId = 16 };
        var there = Enemy(-1, Off(4, 8)) with { Life = 100 };

        var plan = Plan(iop, [Portal(1, Off(2, 0)), Portal(2, Off(2, 8))], [Level(_spells, Blow)], eliotrope, there);

        Assert.Equal([TacticAction.Cast(Blow, 1031, Off(2, 0)), TacticAction.Cast(Blow, 1031, Off(2, 0))], plan);
    }

    [Fact]
    public void ABlowThatLandsStraightIsNotSentThroughPortalsThatLandItNowhere()
    {
        var iop = Caster(Off(0, 0)) with { BreedId = 8, MovementPoints = 0 };
        var beside = Enemy(-1, Off(3, 0)) with { Life = 100 };

        var plan = Plan(iop, [Portal(1, Off(1, 0)), Portal(2, Off(1, 9))], [Level(_spells, Blow)], beside);

        Assert.Equal([TacticAction.Cast(Blow, 1031, Off(3, 0)), TacticAction.Cast(Blow, 1031, Off(3, 0))], plan);
    }

    [Fact]
    public void AnEliotropeSetsTwoPortalsThenStrikesThroughThemAnEnemyItsBlowCannotReachStraight()
    {
        // Seven cells away the enemy is out of the blow's range; a portal beside the Eliotrope and one beside the enemy, in line
        // with it, bring the blow there, cast on either: on the near one it comes out of the far one, and the other way round.
        var eliotrope = Caster(Off(0, 0)) with { BreedId = 16, MovementPoints = 0 };
        var there = Enemy(-1, Off(0, 7)) with { Life = 100 };
        int[] portals = [Off(0, 1), Off(0, 6)];

        var plan = Plan(eliotrope, [], [Level(_spells, Portail), Level(_spells, Blow)], there);

        Assert.Equal([Portail, Portail, Blow], plan.Select(action => action.SpellId));
        Assert.Equal(portals.Order(), plan.Take(2).Select(action => action.Cell).Order());
        Assert.Contains(plan[^1].Cell, portals);
    }

    private static TacticAction[] Plan(Combatant actor, ImmutableArray<CellMark> marks, SpellLevel[] known, params Combatant[] others)
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, .. others], ActorId = actor.Id, Round = 1, Marks = marks };
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [.. known]);

        foreach (var other in others)
            spells = spells.SetItem(other.Id, []);

        var knowledge = new FightKnowledge { Spells = spells };

        return [.. _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) }).Actions];
    }

    [Fact]
    public void APushThroughAPortalPushesAwayFromItsExit()
    {
        var there = Enemy(-1, Off(4, 8)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight([Portal(1, Off(2, 0)), Portal(2, Off(2, 8))], there), Level(_spells, Shove), Off(2, 0));

        Assert.Equal(Off(6, 8), after.Find(there.Id)!.Cell);
    }
}
