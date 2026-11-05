using Avalonia;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Tests.Controls;

public sealed class WorldMapRouteLayerTests
{
    [Fact]
    public void AStepIsANodeAroundItsMapsCentreAndTheAreaPressedIsABitLargerThanIt()
    {
        var bounds = WorldMapRouteLayer.StationBounds(new Point(100, 100), 70);

        Assert.Equal(bounds.Width, bounds.Height);
        Assert.Equal(new Point(100, 100), bounds.Center);
        Assert.InRange(bounds.Width, 16, 40);
    }

    [Fact]
    public void TheNodeStaysSmallAtTheLargestZoomSoThatItNeverCoversTheMap()
    {
        var near = WorldMapRouteLayer.StationBounds(new Point(0, 0), 215);
        var far = WorldMapRouteLayer.StationBounds(new Point(0, 0), 20);

        Assert.True(near.Width <= 30);
        Assert.True(far.Width >= 16);
    }
}
