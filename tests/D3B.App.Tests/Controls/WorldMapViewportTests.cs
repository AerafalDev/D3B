using D3B.App.Controls.WorldMaps;
using D3B.Data.WorldMaps;

namespace D3B.App.Tests.Controls;

public sealed class WorldMapViewportTests
{
    private static WorldMapViewport Viewport()
    {
        var viewport = new WorldMapViewport();

        viewport.Resize(800, 600);

        return viewport;
    }

    [Fact]
    public void APointOfTheViewAndThePixelOfTheImageItShowsConvertBackAndForth()
    {
        var viewport = Viewport();

        viewport.ZoomAt(100, 50, 0.5);
        viewport.Pan(30, -12);

        var (x, y) = viewport.ToImage(640, 480);
        var (viewX, viewY) = viewport.ToView(x, y);

        Assert.Equal((640, 480), (Math.Round(viewX, 6), Math.Round(viewY, 6)));
    }

    [Fact]
    public void ZoomingKeepsThePixelUnderThePointerWhereItIs()
    {
        var viewport = Viewport();

        viewport.Pan(120, 80);

        var before = viewport.ToImage(300, 200);

        viewport.ZoomAt(300, 200, 1.75);

        var after = viewport.ToImage(300, 200);

        Assert.Equal(1.75, viewport.Scale);
        Assert.Equal((before.X, before.Y), (Math.Round(after.X, 6), Math.Round(after.Y, 6)));
    }

    [Fact]
    public void CentringOnAPixelShowsItAtTheMiddleOfTheViewAtTheScaleAsked()
    {
        var viewport = Viewport();

        viewport.CenterOn(4_200, 3_100, 0.8);

        var (x, y) = viewport.ToView(4_200, 3_100);

        Assert.Equal(0.8, viewport.Scale);
        Assert.Equal((400, 300), (Math.Round(x, 6), Math.Round(y, 6)));
    }

    [Fact]
    public void TheScaleIsHeldBetweenTheLeastAndTheMost()
    {
        var viewport = Viewport();

        viewport.ZoomAt(0, 0, 100);

        Assert.Equal(WorldMapViewport.MaxScale, viewport.Scale);

        viewport.ZoomAt(0, 0, 0.0001);

        Assert.Equal(WorldMapViewport.MinScale, viewport.Scale);
    }

    [Fact]
    public void FittingShowsAPartWholeAndCentredInsideTheMargin()
    {
        var viewport = Viewport();

        viewport.Fit(0, 0, 10_000, 9_664, 28);

        var (left, top) = viewport.ToView(0, 0);
        var (right, bottom) = viewport.ToView(10_000, 9_664);

        Assert.Equal(544 / 9_664.0, viewport.Scale, 6);
        Assert.Equal(800 - right, left, 6);
        Assert.Equal(600 - bottom, top, 6);
    }

    [Fact]
    public void PanningCannotLoseTheImageItKeepsSomeOfItInView()
    {
        var viewport = Viewport();

        viewport.Pan(5_000, -5_000);
        viewport.Keep(2_000, 1_000, 100);

        var (left, _) = viewport.ToView(0, 0);
        var (_, bottom) = viewport.ToView(0, 1_000);

        Assert.Equal(700, left);
        Assert.Equal(100, bottom);
    }

    [Fact]
    public void TheVisiblePartOfTheImageIsWhatTheViewsCornersShow()
    {
        var viewport = Viewport();

        viewport.ZoomAt(0, 0, 2);
        viewport.Pan(-200, -100);

        Assert.Equal((100.0, 50.0, 500.0, 350.0), viewport.VisibleImage());
    }

    [Fact]
    public void TheTilesAZoomNeedsAreThoseTheVisiblePartOverlapsRowByRow()
    {
        var level = new WorldMapLevel(50, 2_500, 2_100);

        // 3 columns by 3 rows of 1,024 pixels at half size: 2,048 pixels of the full-size image a tile.
        Assert.Equal([1, 2, 4, 5], WorldMapTiling.TilesIn(level, 1_500, 1_500, 2_600, 2_600));
        Assert.Equal([1], WorldMapTiling.TilesIn(level, -500, -500, 100, 100));
        Assert.Equal([6, 9], WorldMapTiling.TilesIn(level, 4_900, 4_000, 9_000, 9_000));
        Assert.Empty(WorldMapTiling.TilesIn(level, 6_000, 0, 7_000, 100));
    }

    [Fact]
    public void ATileLiesWhereItsPixelsOverTheZoomPutThemInTheFullSizeImage()
    {
        var level = new WorldMapLevel(50, 2_500, 2_100);
        var (x, y, width, height) = WorldMapTiling.Place(level, 6);

        Assert.Equal((4_096.0, 2_048.0, 904.0, 2_048.0), (x, y, width, height));
    }
}
