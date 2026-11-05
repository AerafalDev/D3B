using D3B.Data.WorldMaps;

namespace D3B.Data.Tests.WorldMaps;

public sealed class WorldMapLevelTests
{
    [Fact]
    public void ALevelIsCutIntoTilesOfAThousandAndTwentyFourPixelsNumberedRowByRow()
    {
        // World 24 at zoom 1 (client.md): 2,840 by 2,170, three columns and three rows, the last cropped.
        var level = new WorldMapLevel(100, 2840, 2170);

        Assert.Equal(3, level.Columns);
        Assert.Equal(3, level.Rows);
        Assert.Equal(9, level.TileCount);
        Assert.Equal((0, 0, 1024, 1024), level.TileBounds(1));
        Assert.Equal((2048, 0, 792, 1024), level.TileBounds(3));
        Assert.Equal((0, 1024, 1024, 1024), level.TileBounds(4));
        Assert.Equal((0, 2048, 1024, 122), level.TileBounds(7));
        Assert.Equal((2048, 2048, 792, 122), level.TileBounds(9));
    }

    [Fact]
    public void TheTilesCoverTheImageExactly()
    {
        var level = new WorldMapLevel(80, 8000, 7731);
        long pixels = 0;

        for (var index = 1; index <= level.TileCount; index++)
        {
            var (_, _, width, height) = level.TileBounds(index);

            pixels += (long)width * height;
        }

        Assert.Equal(8000L * 7731, pixels);
    }

    [Fact]
    public void AnImageOfWholeTilesHasNoCroppedOne()
    {
        var level = new WorldMapLevel(50, 2048, 1024);

        Assert.Equal(2, level.Columns);
        Assert.Equal(1, level.Rows);
        Assert.Equal((1024, 0, 1024, 1024), level.TileBounds(2));
    }

    [Fact]
    public void ThePixelsOfATileAreFoundInIt()
    {
        var level = new WorldMapLevel(100, 2840, 2170);

        Assert.Equal(1, level.TileAt(0, 0));
        Assert.Equal(1, level.TileAt(1023, 1023));
        Assert.Equal(2, level.TileAt(1024, 0));
        Assert.Equal(4, level.TileAt(0, 1024));
        Assert.Equal(9, level.TileAt(2839, 2169));
    }

    [Fact]
    public void ATileOrAPixelOutsideTheLevelIsRefused()
    {
        var level = new WorldMapLevel(100, 2840, 2170);

        Assert.Throws<ArgumentOutOfRangeException>(() => level.TileBounds(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => level.TileBounds(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => level.TileAt(2840, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => level.TileAt(0, -1));
    }

    [Fact]
    public void AZoomOrASizeOutOfRangeIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapLevel(0, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapLevel(101, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapLevel(50, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapLevel(50, 10, 0));
    }
}
