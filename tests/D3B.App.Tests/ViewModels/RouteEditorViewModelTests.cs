using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.Bot.Assets;

namespace D3B.App.Tests.ViewModels;

public sealed class RouteEditorViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<(RoutesViewModel Routes, RouteEditorViewModel Editor)> OpenAsync(params long[] maps)
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North", Anchors = [.. maps.Select(map => new RouteAnchor { MapId = map })] }, TestContext.Current.CancellationToken);
        var editor = routes.OpenRoute(route.Id)!;

        await editor.Planned;

        return (routes, editor);
    }

    [Fact]
    public async Task ARoutesAnchorsShowAsStationsAndTheMapsBetweenAsALegDrawnThroughThem()
    {
        var (_, editor) = await OpenAsync(1, 4);

        Assert.Equal([(1, 0), (4, 0)], editor.Scene.Stations.Select(station => (station.X, station.Y)));

        var leg = Assert.Single(editor.Scene.Legs);

        Assert.Equal([(1, 0), (2, 0), (3, 0), (4, 0)], leg.Squares);
        Assert.Equal(WorldMapLegKind.Path, leg.Kind);
        Assert.Equal(("2 maps", 4, 0), (editor.Steps[0].Gap, editor.MapCount, editor.GapCount));
    }

    [Fact]
    public async Task ATraceAddsAnAnchorOnTheMapPressedOnceAndAPaintOnlyWhereAnExitJoinsTheLastOne()
    {
        var (_, editor) = await OpenAsync();

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.TraceSquare(2, 0, isDrag: true);
        editor.TraceSquare(9, 9, isDrag: false);
        await editor.Planned;

        Assert.Equal([1L, 2L], editor.Steps.Select(step => step.MapId));
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task APaintGoesOnFromTheMapItCrossedLastEvenWhereNoExitJoinedTheOneBefore()
    {
        var (_, editor) = await OpenAsync();

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.TraceSquare(4, 0, isDrag: true);
        await editor.Planned;

        Assert.Equal([1L, 4L], editor.Steps.Select(step => step.MapId));
    }

    [Fact]
    public async Task ChoosingAnotherWorldInTheMenuShowsItsLayerAndTheTypedIdOfItsMapSwitchesToIt()
    {
        var (_, editor) = await OpenAsync(1, 2);

        Assert.Equal([1, 2], editor.Worlds.Select(world => world.Id));
        Assert.Equal(2, editor.Scene.Stations.Count);

        editor.Worlds[1].ChooseCommand.Execute(null);

        Assert.Equal(2, editor.Layer);
        Assert.Empty(editor.Scene.Stations);
        Assert.Equal([false, true], editor.Worlds.Select(world => world.IsChosen));
        Assert.Equal([true, true], editor.Steps.Select(step => step.IsElsewhere));

        editor.Worlds[0].ChooseCommand.Execute(null);
        editor.AddText = "00007";
        editor.AddCommand.Execute(null);

        Assert.Equal(2, editor.Layer);
        Assert.Single(editor.Scene.Stations);
    }

    [Fact]
    public async Task APointOfThePathPinnedBecomesAnAnchorBetweenTheTwoItLayBetween()
    {
        var (_, editor) = await OpenAsync(1, 4);

        editor.PinPathPoint(0, 3, 0);
        await editor.Planned;

        Assert.Equal([1L, 3L, 4L], editor.Steps.Select(step => step.MapId));
        Assert.Equal([false, true, false], editor.Steps.Select(step => step.IsPinned));
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public async Task AnAnchorNoPathReachesIsARedGapSaidWithTheStepItStartsFrom()
    {
        var (_, editor) = await OpenAsync(1, 5);

        var leg = Assert.Single(editor.Scene.Legs);

        Assert.Equal(WorldMapLegKind.Unreachable, leg.Kind);
        Assert.True(editor.Steps[0].IsGapBroken);
        Assert.Equal(("No path between step 1 and step 2.", 0, 1), (editor.Problem, editor.ProblemStep, editor.GapCount));
        Assert.True(editor.HasProblem);
    }

    [Fact]
    public async Task ADraggedStationTakesTheMapOfTheCoordinatesItIsLetGoOn()
    {
        var (_, editor) = await OpenAsync(1, 4);

        editor.MoveStation(1, 3, 0);
        await editor.Planned;

        Assert.Equal([1L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal(("1 map", false), (editor.Steps[0].Gap, editor.Steps[0].IsPinned));
    }

    [Fact]
    public async Task CoordinatesTypedAddTheMapThereAndAMapsIdAddsItWhereverItLies()
    {
        var (_, editor) = await OpenAsync();

        editor.AddText = "3, 0";
        editor.AddCommand.Execute(null);
        editor.AddText = "4";
        editor.AddCommand.Execute(null);

        Assert.True(editor.AddFailed);
        Assert.Equal("4", editor.AddText);

        editor.AddText = "[-8 ; 77]";
        editor.AddCommand.Execute(null);

        Assert.True(editor.AddFailed);

        editor.AddText = "00005";
        editor.AddCommand.Execute(null);
        await editor.Planned;

        Assert.Equal([3L, 5L], editor.Steps.Select(step => step.MapId));
        Assert.Equal("", editor.AddText);
        Assert.False(editor.AddFailed);
    }

    [Fact]
    public async Task CoordinatesSeveralMapsShareAskWhichOneAndTheChoiceAddsIt()
    {
        var (_, editor) = await OpenAsync();

        editor.AddText = "2,0";
        editor.AddCommand.Execute(null);

        var choice = Assert.IsType<SharedChoiceViewModel>(editor.Choice);

        Assert.Equal("2 maps at 2,0", choice.Title);
        Assert.Equal([2L, 9L], choice.Items.Select(item => item.MapId));
        Assert.Equal(["World", "Inside"], choice.Items.Select(item => item.Kind));

        choice.ChooseCommand.Execute(choice.Items[1]);
        await editor.Planned;

        Assert.Null(editor.Choice);
        Assert.Equal([9L], editor.Steps.Select(step => step.MapId));
        Assert.Contains("inside", editor.Steps[0].Place, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepRemovedOrChosenMovesTheSelection()
    {
        var (_, editor) = await OpenAsync(1, 2, 3);

        editor.Select(1);

        Assert.Equal([false, true, false], editor.Steps.Select(step => step.IsSelected));
        Assert.True(editor.Scene.Stations[1].IsSelected);

        editor.RemoveSelected();
        await editor.Planned;

        Assert.Equal([1L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public async Task ChangingTheCourseOrWhatToDoWithAnUnreachableMapMakesTheRouteDirtyAndSavingKeepsThem()
    {
        var (routes, editor) = await OpenAsync(1, 2, 3);

        Assert.False(editor.IsDirty);

        editor.SetModeCommand.Execute(RouteMode.OneWay);
        editor.SetUnreachableCommand.Execute(RouteUnreachable.Fail);
        editor.TraceSquare(4, 0, isDrag: false);

        Assert.True(editor.IsDirty);
        Assert.True(routes.IsAnyDirty);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.IsType<RouteAsset>(_store.Assets.Find("r1"));

        Assert.False(editor.IsDirty);
        Assert.Equal((RouteMode.OneWay, RouteUnreachable.Fail), (saved.Mode, saved.Unreachable));
        Assert.Equal([1L, 2L, 3L, 4L], saved.Anchors.Select(anchor => anchor.MapId));
    }

    [Fact]
    public async Task ALoopOfThreeStepsSaysItsWayBackAndOneWayDoesNot()
    {
        var (_, editor) = await OpenAsync(1, 3, 4);

        Assert.Equal(["1 map", "neighbours", "Back to step 1 · 2 maps"], editor.Steps.Select(step => step.Gap));
        Assert.Equal(3, editor.Scene.Legs.Count);
        Assert.True(editor.Scene.Legs[2].IsClosing);

        editor.SetModeCommand.Execute(RouteMode.OneWay);
        await editor.Planned;

        Assert.Equal(["1 map", "neighbours", ""], editor.Steps.Select(step => step.Gap));
        Assert.Equal(2, editor.Scene.Legs.Count);
    }

    [Fact]
    public async Task ARouteWithChangesAsksBeforeItsTabClosesAndOneWithoutClosesAtOnce()
    {
        var (routes, editor) = await OpenAsync(1, 2);
        var closed = new List<string>();

        routes.Closed = route => closed.Add(route.Id);
        editor.TraceSquare(3, 0, isDrag: false);
        editor.CloseCommand.Execute(null);

        Assert.True(routes.Dialogs.IsOpen);
        Assert.Empty(closed);

        routes.Dialogs.Close();
        await editor.SaveCommand.ExecuteAsync(null);
        editor.CloseCommand.Execute(null);

        Assert.Equal(["r1"], closed);
        Assert.Empty(routes.Open);
    }
}
