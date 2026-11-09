using Avalonia;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Tests.Controls;

public sealed class WorldMapHintLayerTests
{
    private const int Zaap = 100;
    private const int Workshop = 70;
    private const int Passage = 10;

    private static WorldMapHint Hint(int id, int x, int y, int priority = Workshop)
    {
        return new WorldMapHint(id, "Hint " + id, "Category", 0, x, y, priority, null);
    }

    private static Func<int, int, Rect> Squares(double width, double dx = 0)
    {
        return (x, y) => new Rect(dx + (x * width), y * width * 0.7, width, width * 0.7);
    }

    [Fact]
    public void AHintStandsInTheMiddleOfItsMapsSquareAtAFixedSizeOnScreen()
    {
        var placed = WorldMapHintLayer.Layout([Hint(1, 3, 2)], Squares(70), 70);

        var only = Assert.Single(placed);

        Assert.Equal(Squares(70)(3, 2).Center, only.Bounds.Center);
        Assert.Equal(only.Bounds.Width, only.Bounds.Height);
        Assert.InRange(only.Bounds.Width, 18, 32);
    }

    [Fact]
    public void TheIconGrowsWithTheMapButNeverBeyondItsRange()
    {
        var near = WorldMapHintLayer.IconSize(300);
        var far = WorldMapHintLayer.IconSize(6);

        Assert.Equal(WorldMapHintLayer.IconSize(500), near);
        Assert.True(far < near);
        Assert.InRange(far, 16, 20);
    }

    [Fact]
    public void WhenTwoIconsWouldOverlapTheMoreUsefulOneIsKept()
    {
        // Squares 8 pixels wide: neighbours' icons, 18 pixels at least, cover each other.
        var placed = WorldMapHintLayer.Layout([Hint(1, 0, 0, Workshop), Hint(2, 1, 0, Zaap), Hint(3, 40, 0, Passage + 60)], Squares(8), 8);

        Assert.Equal([2, 3], placed.Select(item => item.Hint.Id).Order());
    }

    [Fact]
    public void WhatIsKeptDoesNotChangeWhenTheMapIsMovedUnderTheView()
    {
        var hints = Enumerable.Range(0, 60).Select(i => Hint(i, i % 12, i / 12, i % 3 == 0 ? Zaap : Workshop)).ToList();
        var here = WorldMapHintLayer.Layout(hints, Squares(9), 9).Select(item => item.Hint.Id).Order().ToList();
        var moved = WorldMapHintLayer.Layout(hints, Squares(9, 313.5), 9).Select(item => item.Hint.Id).Order().ToList();

        Assert.NotEmpty(here);
        Assert.True(here.Count < hints.Count);
        Assert.Equal(here, moved);
    }

    [Fact]
    public void AHintOfLittleUseWaitsForTheMapsToBeWideEnough()
    {
        var hints = new[] { Hint(1, 0, 0, Passage), Hint(2, 5, 5, Zaap) };

        Assert.Equal([2], WorldMapHintLayer.Layout(hints, Squares(10), 10).Select(item => item.Hint.Id));
        Assert.Equal([1, 2], WorldMapHintLayer.Layout(hints, Squares(70), 70).Select(item => item.Hint.Id).Order());
    }

    [Fact]
    public void HintsOnOneSquareSpreadSideBySideWhenThereIsRoomAndElseTheMostUsefulStays()
    {
        var hints = new[] { Hint(1, 4, 4, Workshop), Hint(2, 4, 4, Zaap), Hint(3, 4, 4, Workshop + 5) };
        var wide = WorldMapHintLayer.Layout(hints, Squares(150), 150);
        var narrow = WorldMapHintLayer.Layout(hints, Squares(20), 20);

        Assert.Equal(3, wide.Count);
        Assert.Equal(3, wide.Select(item => item.Bounds.Center.X).Distinct().Count());
        Assert.All(wide, item => Assert.InRange(item.Bounds.Center.X, Squares(150)(4, 4).Left, Squares(150)(4, 4).Right));
        Assert.Equal([2], narrow.Select(item => item.Hint.Id));
    }

    [Fact]
    public void TheNameOfTheHintUnderThePointerIsWrittenAboveItAndKeptInTheView()
    {
        var view = new Rect(0, 0, 400, 300);
        var icon = new Rect(190, 140, 24, 24);
        var above = WorldMapHintLayer.LabelBox(icon, new Size(80, 14), view);
        var left = WorldMapHintLayer.LabelBox(new Rect(2, 140, 24, 24), new Size(80, 14), view);
        var nearTop = new Rect(190, 2, 24, 24);
        var top = WorldMapHintLayer.LabelBox(nearTop, new Size(80, 14), view);

        Assert.True(above.Bottom <= icon.Top);
        Assert.Equal(icon.Center.X, above.Center.X, 0.5);
        Assert.True(left.Left >= 0);
        Assert.True(top.Top >= nearTop.Bottom);
    }

    [Fact]
    public void ThePointerFindsTheIconUnderIt()
    {
        var hints = new[] { Hint(1, 2, 2, Zaap), Hint(2, 9, 2, Workshop) };
        var placed = WorldMapHintLayer.Layout(hints, Squares(70), 70);
        var over = Squares(70)(9, 2).Center;

        Assert.Equal(2, WorldMapHintLayer.HitAt(placed, over)?.Id);
        Assert.Equal(1, WorldMapHintLayer.HitAt(placed, Squares(70)(2, 2).Center + new Point(3, -4))?.Id);
        Assert.Null(WorldMapHintLayer.HitAt(placed, new Point(5, 400)));
    }
}
