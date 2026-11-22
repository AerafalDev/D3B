using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class MapEntriesTests
{
    // A mine's ledge as the owner's captures measured one (D495): a strip at floor -100 from the top edge down to cell 176, cut by
    // its cliff from the floor 0 the rest of the map lies on, a stair elsewhere making the floors count (D86).
    private static (MapCell[] Cells, IReadOnlyList<int> Ledge) Ledge()
    {
        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(), 6, 176, out var path));

        var cells = TestCells.OpenBut();

        foreach (var cell in path.Cells.Span)
            cells[cell] = TestCells.At(-100, 0);

        cells[500] = TestCells.At(0, 1);

        return (cells, path.Cells.ToArray());
    }

    [Fact]
    public void ALedgeTouchingOnlyTheTopEdgeIsEnteredFromTheNorthAloneByItsOwnCellsOfThatEdge()
    {
        var (cells, ledge) = Ledge();
        var entries = MapEntries.Reaching(cells, [176], 1);

        Assert.Equal([6], entries.Keys);
        Assert.Equal(ledge.Where(cell => cell < 28).Order(), entries[6].Order());
    }

    [Fact]
    public void ACellEveryEdgeReachesIsEnteredFromEverySide()
    {
        var entries = MapEntries.Reaching(TestCells.OpenBut(), [300], 1);

        Assert.Equal([0, 2, 4, 6], entries.Keys.Order());
    }

    [Fact]
    public void CellsOfAnotherZoneEnterNothing()
    {
        Assert.Empty(MapEntries.Reaching(TestCells.OpenBut(), [300], 2));
    }

    [Theory]
    [InlineData(154, 4, 167)]
    [InlineData(167, 0, 154)]
    [InlineData(6, 6, 538)]
    [InlineData(538, 2, 6)]
    public void AScrollOntoACellOfAnEdgeLeavesTheNeighbourFromTheCellStraightAcross(int cell, int side, int left)
    {
        // Measured on the 52,658 scrolls of the captures of 3.7.14.18: leaving east from a cell arrived 13 cells before it, south 532
        // before it, and the other ways round, 46,900 times; the cell straight across could not be walked on for 2,094 of the rest (D495).
        Assert.Equal(left, MapEntries.LeftFrom(cell, side));
    }
}
