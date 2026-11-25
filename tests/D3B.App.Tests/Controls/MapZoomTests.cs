using Avalonia;
using D3B.App.Controls;

namespace D3B.App.Tests.Controls;

public sealed class MapZoomTests
{
    private const double Width = 1000;
    private const double Height = 700;
    private const int Cell = 300;

    private static readonly MapLayout _fitted = MapLayout.Fit(new Size(Width, Height));

    private static MapZoom Zoom()
    {
        var zoom = new MapZoom();

        zoom.Resize(Width, Height);

        return zoom;
    }

    [Fact]
    public void TheMapIsFittedAtFirst()
    {
        var zoom = Zoom();

        Assert.Equal(1, zoom.Scale);
        Assert.Equal(_fitted, zoom.Apply(_fitted));
    }

    [Fact]
    public void TheZoomStaysBetweenTheFitAndFourTimesIt()
    {
        var zoom = Zoom();

        zoom.ZoomAt(500, 350, 10);
        Assert.Equal(4, zoom.Scale);

        zoom.ZoomAt(500, 350, 0.2);
        Assert.Equal(1, zoom.Scale);
        Assert.Equal(_fitted, zoom.Apply(_fitted));
    }

    [Fact]
    public void ZoomingKeepsTheCellUnderThePointerWhereItIs()
    {
        var zoom = Zoom();
        var pointer = _fitted.Center(Cell);

        zoom.ZoomAt(pointer.X, pointer.Y, 2.5);

        var layout = zoom.Apply(_fitted);

        Assert.Equal(2.5 * _fitted.CellWidth, layout.CellWidth, 9);
        Assert.Equal(pointer.X, layout.Center(Cell).X, 9);
        Assert.Equal(pointer.Y, layout.Center(Cell).Y, 9);
        Assert.Equal(Cell, layout.CellAt(pointer));

        // A second zoom elsewhere keeps its own point, the first zoom's offset taken into account.
        var other = layout.Center(Cell + 3);

        zoom.ZoomAt(other.X, other.Y, 3.5);
        Assert.Equal(other.X, zoom.Apply(_fitted).Center(Cell + 3).X, 9);
        Assert.Equal(other.Y, zoom.Apply(_fitted).Center(Cell + 3).Y, 9);
    }

    [Fact]
    public void ADragPansTheZoomedMapButNeverPastItsEdges()
    {
        var zoom = Zoom();

        zoom.ZoomAt(500, 350, 2);
        zoom.Pan(30, -20);

        Assert.Equal((-470, -370), (zoom.OffsetX, zoom.OffsetY));

        // The zoomed control still covers the view: its left edge never leaves the view's left, its right the view's right.
        zoom.Pan(5000, 5000);
        Assert.Equal((0, 0), (zoom.OffsetX, zoom.OffsetY));

        zoom.Pan(-9000, -9000);
        Assert.Equal((Width - (2 * Width), Height - (2 * Height)), (zoom.OffsetX, zoom.OffsetY));
    }

    [Fact]
    public void TheFittedMapDoesNotPan()
    {
        var zoom = Zoom();

        zoom.Pan(120, 80);

        Assert.Equal((0, 0), (zoom.OffsetX, zoom.OffsetY));
    }

    [Fact]
    public void FittingShowsTheWholeMapAgain()
    {
        var zoom = Zoom();

        zoom.ZoomAt(200, 100, 3);
        zoom.Pan(-150, -40);
        zoom.Fit();

        Assert.Equal((1, 0, 0), (zoom.Scale, zoom.OffsetX, zoom.OffsetY));
        Assert.Equal(_fitted, zoom.Apply(_fitted));
    }

    [Fact]
    public void ASmallerViewKeepsTheZoomedMapCoveringIt()
    {
        var zoom = Zoom();

        zoom.ZoomAt(Width, Height, 2);
        Assert.Equal((-Width, -Height), (zoom.OffsetX, zoom.OffsetY));

        zoom.Resize(600, 400);

        Assert.Equal((-600, -400), (zoom.OffsetX, zoom.OffsetY));
    }
}
