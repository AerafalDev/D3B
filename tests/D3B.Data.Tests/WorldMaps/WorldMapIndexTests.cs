using D3B.Data.World;
using D3B.Data.WorldMaps;

namespace D3B.Data.Tests.WorldMaps;

public sealed class WorldMapIndexTests
{
    private const int OutdoorSubArea = 10;
    private const int VaultSubArea = 20;
    private const int LostSubArea = 30;

    private static WorldMapIndex Index()
    {
        MapPosition[] positions =
        [
            // Two maps of world 1 at 3,4: an inside lower than the outside by id, and the outside.
            new(100, 3, 4, OutdoorSubArea, 1, 0, 0),
            new(200, 3, 4, OutdoorSubArea, 1, 0, 0),
            new(300, 5, 5, OutdoorSubArea, 1, 0, 0),
            new(400, 1, 1, OutdoorSubArea, 2, 0, 0),

            // Insides: one whose sub-area lies on world 1, one in a sub-area of its own at coordinates only world 2 holds, one nowhere.
            new(50, 3, 4, VaultSubArea, -1, 0, 0),
            new(60, 1, 1, LostSubArea, -1, 0, 0),
            new(70, 99, 99, LostSubArea, -1, 0, 0),
        ];
        SubArea[] subAreas =
        [
            new(OutdoorSubArea, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0),
            new(VaultSubArea, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0),
            new(LostSubArea, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0),
        ];

        // The vault's sub-area has no map on a world: its inside goes where its coordinates are.
        return new WorldMapIndex(new WorldAtlas(positions.OrderBy(position => position.MapId).ToArray(), subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>()));
    }

    [Fact]
    public void MapsOnTheSameCoordinatesComeTheWorldsOwnFirstThenTheInsidesEachByAscendingId()
    {
        var index = Index();

        Assert.Equal([100L, 200L, 50L], index.MapsAt(1, 3, 4).ToArray());
    }

    [Fact]
    public void AnInsideWithNoWorldInItsSubAreaLivesWhereTheCoordinatesAreOfTheLeastWorldThatHasAMapThere()
    {
        var index = Index();

        Assert.True(index.TryPlace(60, out var layer, out var x, out var y));
        Assert.Equal((2, 1, 1), (layer, x, y));
        Assert.Equal([400L, 60L], index.MapsAt(2, 1, 1).ToArray());
    }

    [Fact]
    public void AnInsideWithNeitherLeavesTheIndex()
    {
        var index = Index();

        Assert.False(index.TryPlace(70, out var layer, out _, out _));
        Assert.Equal(-1, layer);
    }

    [Fact]
    public void ACoordinatesNoMapStandsOnHoldNoneInThatLayer()
    {
        var index = Index();

        Assert.True(index.MapsAt(1, 1, 1).IsEmpty);
        Assert.True(index.MapsAt(7, 3, 4).IsEmpty);
    }

    [Fact]
    public void ALayersSquaresCountTheirMapsAndSayWhichAreShared()
    {
        var squares = Index().SquaresOf(1).ToArray();

        Assert.Equal([new WorldMapSquare(3, 4, 3), new WorldMapSquare(5, 5, 1)], squares);
        Assert.Equal([true, false], squares.Select(square => square.IsShared));
        Assert.True(Index().SquaresOf(9).IsEmpty);
    }
}
