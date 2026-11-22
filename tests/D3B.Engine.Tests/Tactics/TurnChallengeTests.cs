using System.Collections.Immutable;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

public sealed class TurnChallengeTests
{
    private const int Spell = 200;

    public static TheoryData<string> Kept =>
    [
        "TM=1,0", "Tm<1,0", "TM=m,0&TT=0,0", "SAt0", "TD<2,hc0,hc0", "TD>1,hc0,hc0", "TD>1,hc0,e1", "TD<2,hc0,whb", "SP=te,l,hc0,hc0",
        "SP=te,d,hc0,hc0", "SV=te,hc0,e1,s1,a", "SV=te,hc0,e1,s0,v", "TD<2,hc0,e1", "SC=hc0,e,s",
        "SO!e0,hc0,h", "CK~1", "Ck~1", "CK~!,1", "CC=e0,m1,k,k", "CO=e0,m1,tk,n", "SL<1", "SL>1", "SF=1",
    ];

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Standing(int movementPoints, params Combatant[] others)
    {
        var actor = Caster() with { MovementPoints = movementPoints };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, .. others], ActorId = actor.Id, Round = 2 };
        var knowledge = new FightKnowledge { TurnPoints = ImmutableDictionary<long, (int, int)>.Empty.Add(actor.Id, (6, 3)) };

        return (fight, knowledge);
    }

    private static Combatant Ally(int cell)
    {
        return new Combatant { Id = 2, Team = 0, Cell = cell, Life = 100, MaxLife = 100 };
    }

    private static bool Ends(TurnEnd end, ImaginedFight fight)
    {
        return new TurnChallenge { End = end }.IsKept(fight, FightKnowledge.Empty);
    }

    [Theory]
    [MemberData(nameof(Kept))]
    public void TheOwnersCriteriaAreKept(string completion)
    {
        Assert.NotNull(TurnChallenge.Of(completion, 100));
    }

    [Theory]
    [InlineData("CC=e0,m1,gk,d")]
    [InlineData("SD!hc0,e-1")]
    [InlineData("Ma=1")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherCriterionIsNot(string? completion)
    {
        Assert.Null(TurnChallenge.Of(completion, 100));
    }

    [Fact]
    public void TheMostLossIsThreeQuartersOfWhatKeepsTheRewardAMinuteTheSame()
    {
        Assert.Equal(0.375, TurnChallenge.Of("SAt0", 100)!.MostLoss, 6);
        Assert.Equal(0.75 * 0.45 / 1.45, TurnChallenge.Of("SAt0", 45)!.MostLoss, 6);
        Assert.Equal(0, TurnChallenge.Of("SAt0", 0)!.MostLoss);
    }

    [Fact]
    public void StatueIsKeptOnlyWhileNoMovementPointIsUsed()
    {
        var still = new TurnChallenge { Movement = TurnMovement.Still };

        Assert.True(still.IsKept(Standing(3).Fight, Standing(3).Knowledge));
        Assert.False(still.IsKept(Standing(2).Fight, Standing(2).Knowledge));
        Assert.False(still.AllowsWalk(Standing(3).Fight, Standing(3).Knowledge, 2));
    }

    [Fact]
    public void StatueIsBrokenByAMoveThatUsesNoMovementPoint()
    {
        // A jump or a teleport of its own: the fighter leaves its cell with its points whole.
        var still = new TurnChallenge { Movement = TurnMovement.Still, StartCell = Off(2, 0) };

        Assert.False(still.IsKept(Standing(3).Fight, Standing(3).Knowledge));
        Assert.True((still with { StartCell = Middle }).IsKept(Standing(3).Fight, Standing(3).Knowledge));
    }

    [Fact]
    public void AChallengeTheTurnKeptIsKeptWhateverItCosts()
    {
        var kept = TurnChallenge.Of("Tm<1,0", 45)!.KeptThisTurn();

        Assert.Equal(double.PositiveInfinity, kept.MostLoss);
        Assert.Equal(TurnMovement.Still, kept.Movement);
    }

    [Fact]
    public void ZombieIsKeptOnExactlyOneMovementPoint()
    {
        var zombie = new TurnChallenge { Movement = TurnMovement.OnePoint };
        var (fight, knowledge) = Standing(3);

        Assert.False(zombie.IsKept(fight, knowledge));
        Assert.True(zombie.IsKept(Standing(2).Fight, knowledge));
        Assert.False(zombie.IsKept(Standing(1).Fight, knowledge));
        Assert.True(zombie.AllowsWalk(fight, knowledge, 2));
        Assert.False(zombie.AllowsWalk(fight, knowledge, 1));
    }

    [Fact]
    public void NomadeIsKeptOnceEveryMovementPointIsUsed()
    {
        var nomade = new TurnChallenge { Movement = TurnMovement.AllPoints };

        Assert.False(nomade.IsKept(Standing(1).Fight, Standing(1).Knowledge));
        Assert.True(nomade.IsKept(Standing(0).Fight, Standing(0).Knowledge));
        Assert.True(nomade.AllowsWalk(Standing(3).Fight, Standing(3).Knowledge, 1));
    }

    [Fact]
    public void VersatileCastsEachSpellOnceATurn()
    {
        var versatile = new TurnChallenge { EachActionOnce = true };
        var (fight, knowledge) = Standing(3);
        var cast = fight with { History = fight.History.Cast(Spell, null, fight.Round) };
        var twice = cast with { History = cast.History.Cast(Spell, null, fight.Round) };
        var lastTurn = fight with { History = fight.History.Cast(Spell, null, fight.Round - 1) };

        Assert.True(versatile.AllowsCast(fight, Spell));
        Assert.False(versatile.AllowsCast(cast, Spell));
        Assert.True(versatile.AllowsCast(cast, Spell + 1));
        Assert.True(versatile.AllowsCast(lastTurn, Spell));
        Assert.True(versatile.IsKept(cast, knowledge));
        Assert.False(versatile.IsKept(twice, knowledge));
    }

    [Fact]
    public void TheEndsBesideAFighterAreThoseOneCellAway()
    {
        var beside = Standing(3, Ally(Off(1, 0)), Enemy(-1, Off(5, 0))).Fight;
        var apart = Standing(3, Ally(Off(1, 1)), Enemy(-1, Off(0, -1))).Fight;

        Assert.True(Ends(TurnEnd.BesideAnAlly, beside));
        Assert.False(Ends(TurnEnd.AwayFromAllies, beside));
        Assert.True(Ends(TurnEnd.AwayFromEnemies, beside));
        Assert.False(Ends(TurnEnd.BesideAnAlly, apart));
        Assert.True(Ends(TurnEnd.AwayFromAllies, apart));
        Assert.False(Ends(TurnEnd.AwayFromEnemies, apart));
        Assert.False(Ends(TurnEnd.BesideAnEnemy, beside));
        Assert.True(Ends(TurnEnd.BesideAnEnemy, apart));
    }

    [Fact]
    public void AnEndBesideAnObstacleIsBesideACellNotWalkedOn()
    {
        var open = Standing(3).Fight;

        Assert.False(Ends(TurnEnd.BesideAnObstacle, open));
        Assert.True(Ends(TurnEnd.BesideAnObstacle, open with { Map = Open(Off(0, 1)) }));
        Assert.False(Ends(TurnEnd.BesideAnObstacle, open with { Map = Open(Off(1, 1)) }));
    }

    [Fact]
    public void AnEndInLineOrInDiagonalIsOnAnAllysRowColumnOrDiagonal()
    {
        Assert.True(Ends(TurnEnd.InLineWithAnAlly, Standing(3, Ally(Off(4, 0))).Fight));
        Assert.True(Ends(TurnEnd.InLineWithAnAlly, Standing(3, Ally(Off(0, -3))).Fight));
        Assert.False(Ends(TurnEnd.InLineWithAnAlly, Standing(3, Ally(Off(2, 1))).Fight));
        Assert.True(Ends(TurnEnd.InDiagonalWithAnAlly, Standing(3, Ally(Off(2, -2))).Fight));
        Assert.False(Ends(TurnEnd.InDiagonalWithAnAlly, Standing(3, Ally(Off(2, 1))).Fight));
        Assert.False(Ends(TurnEnd.InDiagonalWithAnAlly, Standing(3).Fight));
    }

    [Fact]
    public void AnEndInSightIsOneAVisibleEnemySees()
    {
        var seen = Standing(3, Enemy(-1, Off(4, 0))).Fight;
        var hidden = seen with { Map = WithPillars(Off(2, 0)) };
        var invisible = Standing(3, Enemy(-1, Off(4, 0)) with { IsVisible = false }).Fight;

        Assert.True(Ends(TurnEnd.InSightOfAnEnemy, seen));
        Assert.False(Ends(TurnEnd.OutOfEnemiesSight, seen));
        Assert.False(Ends(TurnEnd.InSightOfAnEnemy, hidden));
        Assert.True(Ends(TurnEnd.OutOfEnemiesSight, hidden));
        Assert.True(Ends(TurnEnd.OutOfEnemiesSight, invisible));
    }

    [Fact]
    public void ADeadFighterKeepsWhatItCannotPlay()
    {
        var (fight, knowledge) = Standing(3);
        var dead = fight.With(fight.Actor with { IsAlive = false });

        Assert.True(new TurnChallenge { Movement = TurnMovement.OnePoint, End = TurnEnd.BesideAnAlly }.IsKept(dead, knowledge));
    }
}
