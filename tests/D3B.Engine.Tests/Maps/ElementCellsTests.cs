using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class ElementCellsTests
{
    [Fact]
    public void AnElementIsUsedFromItsWalkableNeighboursNotFromItsOwnCell()
    {
        // The client's harvest of an element on a walkable cell, 176: it stood on 190, a neighbour (client.md).
        var cells = TestCells.OpenBut(162, 204);

        Assert.Equal([177, 190, 189, 175, 161, 148], ElementCells.UsedFrom(cells, 176, 1));
    }

    [Fact]
    public void WithoutAWalkableNeighbourAnElementIsUsedFromTheNearestWalkableCells()
    {
        // Water all around cell 408, as for the client's fishing, which stood on one of the nearest cells (client.md).
        var cells = TestCells.Where(cell => MapGeometry.Distance(cell, 408) >= 3);
        var used = ElementCells.UsedFrom(cells, 408, 1);

        Assert.NotEmpty(used);
        Assert.All(used, cell => Assert.Equal(3, MapGeometry.Distance(cell, 408)));
        Assert.Equal(Enumerable.Range(0, MapData.CellCount).Count(cell => MapGeometry.Distance(cell, 408) == 3), used.Count);
    }

    [Fact]
    public void OnlyCellsOfTheCharactersZoneCount()
    {
        var cells = TestCells.OpenBut();

        cells[177] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        Assert.DoesNotContain(177, ElementCells.UsedFrom(cells, 176, 1));
        Assert.Equal([177], ElementCells.UsedFrom(cells, 176, 2));
        Assert.Empty(ElementCells.UsedFrom(cells, 176, 3));
    }

    [Fact]
    public void AnElementWhoseOnlyNeighboursAreInAnotherZoneIsNotUsedFromAFarCellOfTheCharactersZone()
    {
        // The element stands in the other zone's height (D424): the zone's cells two steps away are no place to use it from.
        var cells = TestCells.OpenBut();

        foreach (var direction in Enumerable.Range(0, MapCell.DirectionCount))
            cells[MapGeometry.Neighbour(176, direction)] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        Assert.Empty(ElementCells.UsedFrom(cells, 176, 1));
        Assert.Equal(8, ElementCells.UsedFrom(cells, 176, 2).Count);
    }

    [Fact]
    public void WaterAllAroundAnElementStillSendsTheCharacterToTheNearestCellsOfItsOwnZoneOnly()
    {
        var cells = TestCells.Where(cell => MapGeometry.Distance(cell, 408) >= 3);

        foreach (var cell in Enumerable.Range(0, MapData.CellCount).Where(cell => MapGeometry.Distance(cell, 408) == 3))
            cells[cell] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        var used = ElementCells.UsedFrom(cells, 408, 1);

        Assert.NotEmpty(used);
        Assert.All(used, cell => Assert.Equal(4, MapGeometry.Distance(cell, 408)));
    }

    // A mine's map as the owner's captures of 21 November measured it: the ore on a ledge at floor -100, three of its
    // neighbours there, the others up on the floor 0 above, a stair elsewhere on the map.
    private static MapCell[] Ledge()
    {
        var cells = TestCells.OpenBut();

        cells[500] = TestCells.At(0, 1);
        cells[176] = TestCells.At(-100, 0);

        foreach (var neighbour in new[] { 177, 190, 189 })
            cells[neighbour] = TestCells.At(-100, 0);

        return cells;
    }

    [Fact]
    public void OnAMapWithStairsAnElementIsUsedOnlyFromTheNeighboursOnItsFloor()
    {
        // The server refused all 124 uses from a neighbour 100 to 190 above the ore, and took 427 from 40 or less (D495).
        Assert.Equal([177, 190, 189], ElementCells.UsedFrom(Ledge(), 176, 1));
    }

    [Fact]
    public void OnAMapWithStairsAnElementWithNoNeighbourOnItsFloorIsUsedFromAllOfThem()
    {
        var cells = Ledge();

        foreach (var neighbour in new[] { 177, 190, 189 })
            cells[neighbour] = TestCells.At(0, 0);

        Assert.Equal(8, ElementCells.UsedFrom(cells, 176, 1).Count);
    }

    [Fact]
    public void OnAMapWithoutStairsTheFloorsOfAnElementsNeighboursDoNotCount()
    {
        var cells = Ledge();

        cells[500] = TestCells.At(0, 0);

        Assert.Equal(8, ElementCells.UsedFrom(cells, 176, 1).Count);
    }

    [Fact]
    public void AnElementsZonesAreThoseOfTheNeighboursOnItsFloor()
    {
        var cells = Ledge();

        cells[161] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        Assert.Equal([1], ElementCells.NeighbourZones(cells, 176));
    }

    [Fact]
    public void TheZonesOfAnElementsWalkableNeighboursAreListedInAscendingOrder()
    {
        var cells = TestCells.OpenBut(162);

        cells[177] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 3 << 4, 0);
        cells[190] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);
        cells[189] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        Assert.Equal([1, 2, 3], ElementCells.NeighbourZones(cells, 176));
    }

    [Fact]
    public void AnElementWithNoWalkableNeighbourHasNoZone()
    {
        Assert.Empty(ElementCells.NeighbourZones(TestCells.Where(cell => MapGeometry.Distance(cell, 408) >= 3), 408));
    }

    [Fact]
    public void AMapOfAnotherSizeOrACellOffItIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ElementCells.UsedFrom(new MapCell[10], 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElementCells.UsedFrom(TestCells.OpenBut(), MapData.CellCount, 1));
    }
}
