using System.Collections.Immutable;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

/// <summary>An Eliotrope's portals as the references model them (D468): a cast on one comes out of the last of its chain.</summary>
public sealed class PortalsTests
{
    private static readonly Combatant _caster = Caster(Off(0, 0));

    private static CellMark Portal(int id, int cell, int team = 0)
    {
        // A portal of the pack's Portail: its effect 1181's first dice, 2, the damage it adds a cell between two portals.
        return new CellMark(id, 1, team, CellMarkKind.Portal, 2, 44338, cell, [cell], 1_001);
    }

    [Fact]
    public void ACastOnAPortalComesOutOfTheOtherAsFarFromItAsTheCasterIsFromTheFirstTwoPercentACellBetweenThem()
    {
        ImmutableArray<CellMark> marks = [Portal(1, Off(2, 0)), Portal(2, Off(2, 6))];

        var passage = Portals.Through(marks, [_caster], _caster.Cell, Off(2, 0));

        Assert.Equal(new PortalPassage(Off(2, 0), Off(2, 6), Off(4, 6), 12), passage);
    }

    [Fact]
    public void ACastGoesThroughEveryPortalOfTheChainEachToTheNearestLeftAndOutOfTheLast()
    {
        ImmutableArray<CellMark> marks = [Portal(1, Off(2, 8)), Portal(2, Off(2, 0)), Portal(3, Off(2, 3))];

        var passage = Portals.Through(marks, [_caster], _caster.Cell, Off(2, 0));

        Assert.Equal(new PortalPassage(Off(2, 0), Off(2, 8), Off(4, 8), 16), passage);
    }

    [Fact]
    public void APortalAFighterStandsOnIsLeftOutOfTheChainAndLetsNoCastIn()
    {
        var standing = Enemy(-1, Off(2, 3));
        ImmutableArray<CellMark> marks = [Portal(1, Off(2, 0)), Portal(2, Off(2, 3)), Portal(3, Off(2, 8))];

        Assert.Equal(new PortalPassage(Off(2, 0), Off(2, 8), Off(4, 8), 16), Portals.Through(marks, [_caster, standing], _caster.Cell, Off(2, 0)));
        Assert.Null(Portals.Through(marks, [_caster, standing], _caster.Cell, Off(2, 3)));
    }

    [Fact]
    public void APortalAloneOrBesideOnlyTheOtherTeamsLetsNoCastThrough()
    {
        Assert.Null(Portals.Through([Portal(1, Off(2, 0))], [_caster], _caster.Cell, Off(2, 0)));
        Assert.Null(Portals.Through([Portal(1, Off(2, 0)), Portal(2, Off(2, 6), team: 1)], [_caster], _caster.Cell, Off(2, 0)));
    }

    [Fact]
    public void ACastOnAnotherCellOrComingOutOffTheMapGoesThroughNoPortal()
    {
        ImmutableArray<CellMark> marks = [Portal(1, Off(2, 0)), Portal(2, At(0, 0))];

        Assert.Null(Portals.Through(marks, [_caster], _caster.Cell, Off(3, 0)));
        Assert.Null(Portals.Through(marks, [_caster with { Cell = Off(8, 0) }], Off(8, 0), Off(2, 0)));
    }

    [Fact]
    public void AFifthPortalOfATeamTakesThePlaceOfItsOldest()
    {
        ImmutableArray<CellMark> marks = [Portal(1, Off(1, 0)), Portal(2, Off(2, 0), team: 1), Portal(3, Off(3, 0)), Portal(4, Off(4, 0)), Portal(5, Off(5, 0))];

        var placed = Portals.Placed(marks, Portal(-8, Off(6, 0)));

        Assert.Equal([2, 3, 4, 5, -8], placed.Select(mark => mark.Id));
        Assert.Equal(6, Portals.Placed(placed, Portal(-9, Off(7, 0), team: 1)).Length);
    }
}
