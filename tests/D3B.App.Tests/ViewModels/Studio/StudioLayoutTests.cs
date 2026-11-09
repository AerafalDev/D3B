using D3B.App.Hosting;
using D3B.App.ViewModels.Studio;
using D3B.Data.Tests;

namespace D3B.App.Tests.ViewModels.Studio;

public sealed class StudioLayoutTests
{
    [Fact]
    public void EachEdgeStaysWithinItsBoundsAsItIsDragged()
    {
        var layout = new StudioLayout();

        layout.ResizeLeft(100, 1440);
        Assert.Equal(StudioLayout.LeastLeftWidth, layout.LeftWidth);

        layout.ResizeLeft(900, 1440);
        Assert.Equal(1440 * 0.4, layout.LeftWidth);

        layout.ResizeRight(1000, 1440);
        Assert.Equal(1440 * 0.45, layout.RightWidth);

        layout.ResizeShare(0.05, 600);
        Assert.Equal(0.2, layout.LeftShare, 3);

        layout.ResizeShare(0.99, 600);
        Assert.Equal(0.8, layout.LeftShare, 3);

        layout.ResizeBottom(40, 700);
        Assert.Equal(StudioLayout.LeastBottomHeight, layout.BottomHeight);

        layout.ResizeBottom(650, 700);
        Assert.Equal(700 - StudioLayout.LeastEditorHeight, layout.BottomHeight);
    }

    [Fact]
    public void AClosedPanelLeavesItsRoomAndTheLeftColumnGoesWithBothOfItsPanels()
    {
        var layout = new StudioLayout();

        layout.Close(StudioPanel.Outliner);

        Assert.Equal((true, false), (layout.IsLeftOpen, layout.AreBothLeftOpen));

        layout.Close(StudioPanel.Assets);

        Assert.False(layout.IsLeftOpen);

        layout.Toggle(StudioPanel.Assets);
        layout.Toggle(StudioPanel.Bottom);

        Assert.Equal((true, true, false), (layout.IsAssetsOpen, layout.IsLeftOpen, layout.IsBottomOpen));
    }

    [Fact]
    public void TheLayoutIsRememberedAsItWasLeftAndTheDefaultIsNotWritten()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        var layout = new StudioLayout(AppPreferences.Load(path));

        layout.ResizeLeft(260, 1440);

        // A drag is remembered once it ends, not at each step.
        Assert.Null(AppPreferences.Load(path).Layout);

        layout.Remember();
        layout.Close(StudioPanel.Bottom);

        var again = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((260d, false, true), (again.LeftWidth, again.IsBottomOpen, again.IsDetailsOpen));
        Assert.False(again.IsDefault);

        again.Reset();

        Assert.True(again.IsDefault);
        Assert.Null(AppPreferences.Load(path).Layout);
        Assert.Equal((StudioLayout.DefaultLeftWidth, true), (new StudioLayout(AppPreferences.Load(path)).LeftWidth, again.IsBottomOpen));
    }

    [Fact]
    public void AFilesLayoutBeyondTheBoundsIsBroughtBackWithinThem()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        // Written by hand, or by an older D3B with other bounds.
        File.WriteAllText(path, """{ "Layout": { "LeftWidth": 10, "RightWidth": -3, "LeftShare": 3, "BottomHeight": -5, "Closed": ["Outliner", "Nothing"] } }""");

        var layout = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((StudioLayout.LeastLeftWidth, StudioLayout.LeastRightWidth, 0.9, StudioLayout.LeastBottomHeight), (layout.LeftWidth, layout.RightWidth, layout.LeftShare, layout.BottomHeight));
        Assert.Equal((false, true), (layout.IsOutlinerOpen, layout.IsAssetsOpen));
    }

    [Fact]
    public void TheEditorOpensEachTabOnceAndClosingOneShowsItsNeighbour()
    {
        var tabs = new EditorTabs();

        EditorTab Tab(string key)
        {
            return new EditorTab(key, () => key, null, key, needsHost: false);
        }

        tabs.Open("a", () => Tab("a"));
        tabs.Open("b", () => Tab("b"));
        tabs.Open("c", () => Tab("c"));
        tabs.Open("a", () => throw new InvalidOperationException("Opened twice."));

        Assert.Equal(["a", "b", "c"], tabs.Items.Select(tab => tab.Key));
        Assert.Equal(("a", true), (tabs.Selected?.Key, tabs.Items[0].IsSelected));

        tabs.Select(tabs.Items[2]);
        tabs.Close(tabs.Items[2]);

        Assert.Equal("b", tabs.Selected?.Key);

        tabs.Close(tabs.Items[1]);
        tabs.Close(tabs.Items[0]);

        Assert.True(tabs.IsEmpty);
        Assert.Null(tabs.Selected);
    }
}
