using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.WorldMaps;
using static D3B.Extractor.Tests.WorldMaps.TestTiles;

namespace D3B.Extractor.Tests.WorldMaps;

/// <summary>How the worlds' zooms are read and their sizes held against their tiles, on records built by hand, so that CI covers the checks without a client.</summary>
public sealed class WorldSizeTests
{
    [Fact]
    public void ZoomsAreWholePercentagesTheLargestFirst()
    {
        Assert.Equal([100, 50, 20], WorldMapTables.ZoomsOf(WorldRecord(1, 10, 10, 0.2f, 1, 0.5f)));
    }

    [Theory]
    [InlineData(0.255f)]
    [InlineData(0f)]
    [InlineData(1.01f)]
    [InlineData(0.5f, 0.5f)]
    public void AZoomThatIsNotAWholePercentageOnceFrom1To100IsRefused(params float[] zooms)
    {
        var error = Assert.Throws<InvalidDataException>(() => WorldMapTables.ZoomsOf(WorldRecord(1, 10, 10, zooms)));

        Assert.Contains("which is not a whole percentage, once each, from 1 to 100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWorldWithoutZoomIsRefused()
    {
        var error = Assert.Throws<InvalidDataException>(() => WorldMapTables.ZoomsOf(WorldRecord(1, 10, 10)));

        Assert.Contains("lists no zoom", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWorldIsSizedByItsTilesAtEachZoom()
    {
        var record = WorldRecord(1, 2000, 1100, 1, 0.5f);
        var tiles = Cut(1, 100, 2000, 1100).Concat(Cut(1, 50, 1000, 550)).ToDictionary();

        var world = Assert.Single(Worlds([record], tiles));

        Assert.Equal([(100, 2000, 1100), (50, 1000, 550)], world.Zooms);
        Assert.Equal((1, (Text?)null, 6480, 4944, 2000, 1100, true), (world.Id, world.Name, world.OriginX, world.OriginY, world.Width, world.Height, world.Visible));
        Assert.Equal((69.5f, 49.7f, 0.2f, 1f, 0.6f), (world.MapWidth, world.MapHeight, world.MinScale, world.MaxScale, world.StartScale));
    }

    [Fact]
    public void WorldsAreInOrderOfId()
    {
        var tiles = Cut(2, 100, 10, 10).Concat(Cut(1, 100, 10, 10)).ToDictionary();

        Assert.Equal([1, 2], Worlds([WorldRecord(2, 10, 10, 1), WorldRecord(1, 10, 10, 1)], tiles).Select(world => world.Id));
    }

    [Fact]
    public void AWorldAFewPixelsOffItsTableTakesItsTilesSize()
    {
        var world = Assert.Single(Worlds([WorldRecord(1, 2000, 1100, 1)], Cut(1, 100, 1993, 1093).ToDictionary()));

        Assert.Equal([(100, 1993, 1093)], world.Zooms);
    }

    [Theory]
    [InlineData(1992, 1100)]
    [InlineData(2000, 1092)]
    [InlineData(2008, 1100)]
    public void AWorldEightPixelsOffItsTableIsRefused(int width, int height)
    {
        Refused([WorldRecord(1, 2000, 1100, 1)], Cut(1, 100, width, height).ToDictionary(), $"is {width}x{height} by its tiles, where its table says 2000x1100");
    }

    [Fact]
    public void AMissingTileOrOneOfAnotherSizeIsRefused()
    {
        var tiles = Cut(1, 100, 2000, 1100).ToDictionary();

        tiles.Remove((1, 100, 4));
        Refused([WorldRecord(1, 2000, 1100, 1)], tiles, "Tile 4 of world 1 at 100 % is missing or not 976x76");

        tiles[(1, 100, 4)] = (976, 80, 100);
        Refused([WorldRecord(1, 2000, 1100, 1)], tiles, "Tile 4 of world 1 at 100 % is missing or not 976x76");

        tiles = Cut(1, 100, 2000, 1100).ToDictionary();
        tiles[(1, 100, 2)] = (976, 1000, 100);
        Refused([WorldRecord(1, 2000, 1100, 1)], tiles, "Tile 2 of world 1 at 100 % is missing or not 976x1024");
    }

    [Fact]
    public void ATileNoZoomCutsIsRefused()
    {
        var tiles = Cut(1, 100, 2000, 1100).ToDictionary();

        tiles[(1, 100, 5)] = (1024, 1024, 100);
        Refused([WorldRecord(1, 2000, 1100, 1)], tiles, "The bundle holds 5 tiles, which the worlds' zooms do not cut");
    }

    /// <summary>The tiles that cut a level of this size, row by row from the top left, each 1,024 pixels but the last of a row or column.</summary>
    private static IEnumerable<KeyValuePair<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)>> Cut(int world, int zoom, int width, int height)
    {
        var columns = WorldMapTables.Columns(width);

        for (var index = 1; index <= columns * WorldMapTables.Columns(height); index++)
        {
            var (column, row) = ((index - 1) % columns, (index - 1) / columns);
            var size = (Math.Min(WorldMap.TileSize, width - (column * WorldMap.TileSize)), Math.Min(WorldMap.TileSize, height - (row * WorldMap.TileSize)), 100);

            yield return new((world, zoom, index), size);
        }
    }

    private static List<WorldMap> Worlds(Fields[] records, Dictionary<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)> tiles)
    {
        return WorldMapTables.Worlds(records, NoTexts(), records.ToDictionary(record => record.Int32("id"), WorldMapTables.ZoomsOf), tiles);
    }

    private static void Refused(Fields[] records, Dictionary<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)> tiles, string reason)
    {
        var error = Assert.Throws<InvalidDataException>(() => Worlds(records, tiles));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }
}
