using D3B.App.Hosting;
using D3B.App.ViewModels.Studio;
using D3B.Data.Tests;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
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

        layout.ResizeBottom(40, 700);
        Assert.Equal(StudioLayout.LeastBottomHeight, layout.BottomHeight);

        layout.ResizeBottom(650, 700);
        Assert.Equal(700 - StudioLayout.LeastEditorHeight, layout.BottomHeight);
    }

    [Fact]
    public void TheStudioOpensOnTheCharactersAloneBesideTheEditor()
    {
        var layout = new StudioLayout();

        Assert.Equal((true, SideView.Characters), (layout.IsSideOpen, layout.View));
        Assert.Equal((false, false, false), (layout.IsBottomOpen, layout.IsDetailsOpen, layout.AdvancedTools));
        Assert.Equal((true, false, false), (layout.IsCharactersShown, layout.IsLibraryShown, layout.IsWorldShown));
    }

    [Fact]
    public void AViewChosenInTheActivityBarShowsAndChosenAgainHidesTheSideBar()
    {
        var layout = new StudioLayout();

        layout.ShowView(SideView.Library);

        Assert.Equal((true, SideView.Library, true, false), (layout.IsSideOpen, layout.View, layout.IsLibraryShown, layout.IsCharactersShown));

        layout.ShowView(SideView.Library);

        Assert.Equal((false, SideView.Library, false), (layout.IsSideOpen, layout.View, layout.IsLibraryShown));

        layout.ShowView(SideView.World);

        Assert.Equal((true, SideView.World, true), (layout.IsSideOpen, layout.View, layout.IsWorldShown));

        // Shown from elsewhere, as Ctrl Space does, a view never hides the side bar.
        layout.Reveal(SideView.World);

        Assert.True(layout.IsSideOpen);
    }

    [Fact]
    public void AClosedPanelOpensAgainByItsToggle()
    {
        var layout = new StudioLayout();

        layout.Close(StudioPanel.Side);
        layout.Toggle(StudioPanel.Bottom);
        layout.Toggle(StudioPanel.Details);

        Assert.Equal((false, true, true), (layout.IsSideOpen, layout.IsBottomOpen, layout.IsDetailsOpen));

        layout.Toggle(StudioPanel.Side);

        Assert.True(layout.IsSideOpen);
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
        layout.Show(StudioPanel.Bottom, true);
        layout.ShowView(SideView.World);
        layout.ToggleAdvancedTools();

        var again = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((260d, true, false, SideView.World, true), (again.LeftWidth, again.IsBottomOpen, again.IsDetailsOpen, again.View, again.AdvancedTools));
        Assert.False(again.IsDefault);

        // The advanced tools are a preference, not a part of the layout: the default layout keeps them.
        again.ToggleAdvancedTools();
        again.Reset();

        Assert.True(again.IsDefault);
        Assert.Null(AppPreferences.Load(path).Layout);
        Assert.Equal((StudioLayout.DefaultLeftWidth, false, SideView.Characters), (new StudioLayout(AppPreferences.Load(path)).LeftWidth, again.IsBottomOpen, again.View));
    }

    [Fact]
    public void AFilesLayoutBeyondTheBoundsIsBroughtBackWithinThem()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        // Written by hand, or by an older D3B with other bounds.
        File.WriteAllText(path, """{ "Layout": { "LeftWidth": 10, "RightWidth": -3, "BottomHeight": -5, "Shown": ["Bottom", "Nothing"], "View": "Nowhere" } }""");

        var layout = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((StudioLayout.LeastLeftWidth, StudioLayout.LeastRightWidth, StudioLayout.LeastBottomHeight), (layout.LeftWidth, layout.RightWidth, layout.BottomHeight));
        Assert.Equal((false, true, false, SideView.Characters), (layout.IsSideOpen, layout.IsBottomOpen, layout.IsDetailsOpen, layout.View));
    }

    [Fact]
    public void TheDetailsShownAreNotRememberedSoThatTheNextStartOpensThemOnNothing()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        var layout = new StudioLayout(AppPreferences.Load(path));

        // As a selection shows them: kept, they would open the next start with nothing selected.
        layout.Show(StudioPanel.Details, true);

        Assert.Null(AppPreferences.Load(path).Layout);

        layout.Show(StudioPanel.Bottom, true);

        var again = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((true, false), (again.IsBottomOpen, again.IsDetailsOpen));
    }

    [Fact]
    public void AnOlderFilesFourDocksGiveTheSideBarAndTheNewDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        // Before D513: the Outliner over the assets, every panel shown but those closed.
        File.WriteAllText(path, """{ "Layout": { "LeftWidth": 280, "RightWidth": 360, "LeftShare": 0.5, "BottomHeight": 200, "Closed": ["Details"] } }""");

        var layout = new StudioLayout(AppPreferences.Load(path));

        Assert.Equal((280d, 360d, 200d), (layout.LeftWidth, layout.RightWidth, layout.BottomHeight));
        Assert.Equal((true, false, false), (layout.IsSideOpen, layout.IsBottomOpen, layout.IsDetailsOpen));

        File.WriteAllText(path, """{ "Layout": { "LeftWidth": 280, "RightWidth": 360, "LeftShare": 0.5, "BottomHeight": 200, "Closed": ["Outliner", "Assets"] } }""");

        Assert.False(new StudioLayout(AppPreferences.Load(path)).IsSideOpen);
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
