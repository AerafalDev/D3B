using D3B.Data.WorldMaps;
using MemoryPack;

namespace D3B.Data.Tests.WorldMaps;

public sealed class WorldMapCatalogTests
{
    private static readonly WorldMapLevel[] _levels =
    [
        new(100, 10000, 9664),
        new(80, 8000, 7731),
        new(20, 2000, 1933),
        new(100, 2840, 2170),
        new(75, 2130, 1628),
        new(50, 1420, 1085),
        new(25, 710, 543),
    ];

    private static readonly WorldMapLevel[] _rising = [new(50, 10, 10), new(75, 15, 15)];
    private static readonly WorldMapLevel[] _repeated = [new(50, 10, 10), new(50, 10, 10)];

    [Fact]
    public void AWorldIsFoundByItsIdentifierWithItsOwnZooms()
    {
        var catalog = Catalog();

        Assert.True(catalog.TryGet(24, out var world));
        Assert.Equal([100, 75, 50, 25], catalog.LevelsOf(world).ToArray().Select(level => level.ZoomPercent));
        Assert.True(catalog.TryGet(1, out var main));
        Assert.Equal([100, 80, 20], catalog.LevelsOf(main).ToArray().Select(level => level.ZoomPercent));
        Assert.False(catalog.TryGet(23, out _));
    }

    [Fact]
    public void TheZoomToDrawIsTheSmallestOneAtLeastAsLargeAsTheScale()
    {
        var catalog = Catalog();

        Assert.True(catalog.TryGet(24, out var world));
        Assert.Equal(100, catalog.LevelFor(world, 1.3).ZoomPercent);
        Assert.Equal(100, catalog.LevelFor(world, 1).ZoomPercent);
        Assert.Equal(100, catalog.LevelFor(world, 0.8).ZoomPercent);
        Assert.Equal(75, catalog.LevelFor(world, 0.75).ZoomPercent);
        Assert.Equal(50, catalog.LevelFor(world, 0.4).ZoomPercent);
        Assert.Equal(25, catalog.LevelFor(world, 0.25).ZoomPercent);
        Assert.Equal(25, catalog.LevelFor(world, 0.05).ZoomPercent);
    }

    [Fact]
    public void AMapStandsOnTheImageWhereItsOriginAndSizeSayAtFullSize()
    {
        var catalog = Catalog();

        Assert.True(catalog.TryGet(1, out var world));
        Assert.Equal((6480 + (4 * 69.5), 4944 - (19 * (double)49.7f)), world.PixelOf(4, -19));
        Assert.Equal((6480d, 4944d), world.PixelOf(0, 0));
    }

    [Fact]
    public void ATileHasANameInThePack()
    {
        Assert.Equal("worldmaps/1/80/37", WorldMapCatalog.TileEntryName(1, 80, 37));
    }

    [Fact]
    public void WorldsOutOfOrderAreRejected()
    {
        var worlds = new[] { World(24, 3, 4), World(1, 0, 3) };

        Assert.Throws<ArgumentException>(() => new WorldMapCatalog(worlds, _levels));
    }

    [Fact]
    public void ZoomsThatDoNotFollowOneAnotherAreRejected()
    {
        var worlds = new[] { World(1, 0, 3), World(24, 4, 3) };

        Assert.Throws<ArgumentException>(() => new WorldMapCatalog(worlds, _levels));
    }

    [Fact]
    public void ZoomsThatDoNotGoDownAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorldMapCatalog(new[] { World(1, 0, 2) }, _rising));
        Assert.Throws<ArgumentException>(() => new WorldMapCatalog(new[] { World(1, 0, 2) }, _repeated));
    }

    [Fact]
    public void ACatalogSurvivesTheWayItIsStored()
    {
        var catalog = Catalog();
        var read = MemoryPackSerializer.Deserialize<WorldMapCatalog>(MemoryPackSerializer.Serialize(catalog))!;

        Assert.Equal(catalog.Worlds.Length, read.Worlds.Length);
        Assert.Equal(catalog.Levels.Length, read.Levels.Length);
        Assert.True(read.TryGet(1, out var world));
        Assert.Equal(6480, world.OriginX);
        Assert.Equal(69.5f, world.MapWidth);
        Assert.True(world.Visible);
        Assert.Equal(_levels[1].Width, read.LevelsOf(world)[1].Width);
    }

    private static WorldMapCatalog Catalog()
    {
        return new WorldMapCatalog(new[] { World(1, 0, 3, 6480, 4944, 69.5f, 49.7f), World(24, 3, 4) }, _levels);
    }

    private static WorldMap World(int id, int firstLevel, int levelCount, int originX = 790, int originY = 1165, float mapWidth = 289, float mapHeight = 195)
    {
        return new WorldMap(id, 460_000 + id, originX, originY, mapWidth, mapHeight, 0.2f, 1, 0.6f, 10000, 9664, true, firstLevel, levelCount);
    }
}
