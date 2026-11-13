using D3B.Extractor.Maps;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Maps;

/// <summary>The map reader on maps built by hand, so that CI covers its checks without a client.</summary>
public sealed class MapReaderTests
{
    [Fact]
    public void AClientMapDataIsReadWithItsNeighboursCellsAndElements()
    {
        var map = new TestMap
        {
            Cell = (cell, field) => (cell, field) switch
            {
                (0, "mov" or "los") => 1,
                (1, "los") => 1,
                (0, "linkedZone") => 0x10,
                (0, "mapChangeData") => 131,
                (12, "altitude") => -20,
                (_, "groundSlope") => 3,
                _ => 0,
            },
            Groups = [(300, [1, 3]), (301, [2])],
            References =
            [
                (1, "Dofus.InteractiveElement", [465453, 300, 7506]),
                (2, "Dofus.GroupedElement", [465454, 7507]),
                (3, "Dofus.Decor", [9000, 5]),
                (4, "Dofus.GroupedElement", [465455, 7508]),
                (5, "Dofus.InteractiveElement", [465456, 400, 7509]),
            ],
        };

        var read = TestMap.Reader().Read(map.Build());

        Assert.Equal((191106007, 191106008, 191106006, 0, 191106520), (read.Id, read.Top, read.Bottom, read.Left, read.Right));
        Assert.Equal((3, 2, 0), (read.Flags[0], read.Flags[1], read.Flags[2]));
        Assert.Equal((0x10, 0), (read.LinkedZone[0], read.LinkedZone[1]));
        Assert.Equal(["floor", "mapChange"], read.Numbers.Keys.Order());
        Assert.Equal(new SortedDictionary<int, int> { [0] = 131 }, read.Numbers["mapChange"]);
        Assert.Equal(new SortedDictionary<int, int> { [12] = -20 }, read.Numbers["floor"]);

        // An element takes its own cell, else its group's, else none; a reference of no interactive id is no element.
        Assert.Equal([(465453, 300, 7506), (465454, 301, 7507), (465455, -1, 7508), (465456, 400, 7509)], read.Elements);
    }

    [Fact]
    public void AReaderReadsEachMapAfresh()
    {
        // The extraction reads every map of a file with one reader: nothing of a map may carry over to the next.
        var reader = TestMap.Reader();

        reader.Read(new TestMap { Groups = [(301, [2])], References = [(1, "Dofus.InteractiveElement", [465453, 300, 7506]), (2, "Dofus.GroupedElement", [465454, 7507])] }.Build());
        reader.Read(new TestMap { Nested = true, Name = "map_1", Boxes = [(5, 9, 10)] }.Build());

        var read = reader.Read(new TestMap { References = [(2, "Dofus.GroupedElement", [465454, 7507])] }.Build());

        Assert.Equal([(465454, -1, 7507)], read.Elements);
    }

    [Fact]
    public void AMapMetadataTakesItsIdFromItsNameAndItsElementsFromItsListAndBoxes()
    {
        var map = new TestMap
        {
            Nested = true,
            Name = "map_54164787",
            Cell = (cell, field) => (cell, field) is (7, "floor") ? 40 : 0,
            Listed = [1],
            Boxes = [(465460, 7510, 200)],
            References = [(1, "Dofus.MapInteractive", [465453, 300, 7506]), (2, "Dofus.InteractiveElement", [465454, 301, 7507])],
        };

        var read = TestMap.Reader().Read(map.Build());

        Assert.Equal(54164787, read.Id);
        Assert.Equal(new SortedDictionary<int, int> { [7] = 40 }, read.Numbers["floor"]);

        // The registry's element the map does not list is not one of its elements.
        Assert.Equal([(465453, 300, 7506), (465460, 200, 7510)], read.Elements);
    }

    [Fact]
    public void AnElementDrawnTwiceOnItsCellIsKeptOnceWithItsLowestGraphic()
    {
        var map = new TestMap { Nested = true, Name = "map_1", Boxes = [(5, 9, 10), (5, 7, 10), (6, 8, 11), (6, 8, 11)] };
        var reader = TestMap.Reader();

        var read = reader.Read(map.Build());

        Assert.Equal([(5, 10, 7), (6, 11, 8)], read.Elements);
        Assert.Equal(1, reader.TwoGraphics);
    }

    [Fact]
    public void BothLayoutsAreMapsAndAnElementsDefinitionIsNot()
    {
        Assert.True(MapReader.IsMap(new TestMap().Build().Tree));
        Assert.True(MapReader.IsMap(new TestMap { Nested = true }.Build().Tree));
        Assert.False(MapReader.IsMap(TestMap.ReferenceTypes["Dofus.InteractiveElement"]));
    }

    [Theory]
    [InlineData("cellsData", "middlegroundMapElements")]
    [InlineData("id", "middlegroundMapElements", "references")]
    [InlineData("id", "cellsData", "references")]
    [InlineData("id", "cellsData", "middlegroundMapElements")]
    public void AClientMapDataWithoutOneOfItsPartsIsNoMap(params string[] fields)
    {
        Assert.False(MapReader.IsMap(Of([.. fields.Select(field => Node(1, "int", field, 4))])));
    }

    [Fact]
    public void AMapMetadataWithoutCellsOrRegistryIsNoMap()
    {
        Assert.False(MapReader.IsMap(Of([Node(1, "MapData", "mapData"), Node(2, "int", "cellsData", 4)])));
        Assert.False(MapReader.IsMap(Of([Node(1, "MapData", "mapData"), Node(2, "int", "boundingBoxes", 4), Node(1, "int", "references", 4)])));
    }

    [Fact]
    public void AMapOfAnotherNumberOfCellsIsRefused()
    {
        Refused(new TestMap { Cells = MapData.CellCount - 1 }, "has 559 cells");
    }

    [Fact]
    public void AMapWithoutCellsIsRefused()
    {
        Refused(new TestMap { Cells = null }, "has no cells");
    }

    [Theory]
    [InlineData("cellNumber")]
    [InlineData("mapChangeData")]
    [InlineData("linkedZone")]
    [InlineData("mov")]
    [InlineData("los")]
    public void ACellWithoutAFieldTheReaderNeedsIsRefused(string missing)
    {
        string[] fields = ["cellNumber", "mapChangeData", "linkedZone", "altitude", "mov", "los"];

        Refused(new TestMap { CellFields = [.. fields.Where(field => field != missing)] }, $"a cell has no {missing}");
    }

    [Fact]
    public void ACellWithTwoAltitudesOrNoneIsRefused()
    {
        string[] fields = ["cellNumber", "mapChangeData", "linkedZone", "mov", "los"];

        Refused(new TestMap { CellFields = fields }, "no altitude, or two");
        Refused(new TestMap { CellFields = [.. fields, "altitude", "floor"] }, "no altitude, or two");
    }

    [Fact]
    public void CellsOutOfOrderAreRefused()
    {
        Refused(new TestMap { CellNumber = cell => cell is 3 ? 4 : cell }, "cell 3 is numbered otherwise");
    }

    [Fact]
    public void ALinkedZoneBeyondAByteIsRefused()
    {
        var map = new TestMap { Cell = (cell, field) => (cell, field) is (0, "linkedZone") ? 256 : 0 }.Build();

        Assert.Throws<OverflowException>(() => TestMap.Reader().Read(map));
    }

    [Fact]
    public void AnInteractiveElementWithoutAGraphicIsRefused()
    {
        Refused(new TestMap { References = [(1, "Dofus.Blank", [465453])] }, "interactive element 465453 names no graphic");
    }

    [Fact]
    public void AnElementOnTwoCellsIsRefused()
    {
        Refused(new TestMap { Nested = true, Name = "map_1", Boxes = [(5, 9, 10), (5, 9, 11)] }, "on cells 10 and 11");
    }

    [Fact]
    public void AnElementWhoseCellIsNotItsGroupsIsRefused()
    {
        var map = new TestMap { Groups = [(11, [1])], References = [(1, "Dofus.InteractiveElement", [465453, 10, 7506])] };

        Refused(map, "names cell 10, its group cell 11");
    }

    [Theory]
    [InlineData("")]
    [InlineData("map_")]
    [InlineData("map_-1")]
    [InlineData("map_12a")]
    [InlineData("mapdata_12")]
    public void AMapMetadataNotNamedAfterItsIdIsRefused(string name)
    {
        Refused(new TestMap { Nested = true, Name = name }, "not map_<id>");
    }

    [Fact]
    public void ANegativeIdIsRefused()
    {
        Refused(new TestMap { Id = -1 }, "ids run from 0");
    }

    [Fact]
    public void AListedElementTheRegistryLacksIsRefused()
    {
        Refused(new TestMap { Nested = true, Name = "map_1", Listed = [7] }, "lists interactive element 7, which its registry does not hold");
    }

    [Fact]
    public void BytesLeftAfterTheMapAreRefused()
    {
        Refused(new TestMap { Trailing = [0, 0, 0, 0] }, "4 bytes are left");
    }

    private static void Refused(TestMap map, string reason)
    {
        var built = map.Build();
        var error = Assert.Throws<InvalidDataException>(() => TestMap.Reader().Read(built));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }
}
