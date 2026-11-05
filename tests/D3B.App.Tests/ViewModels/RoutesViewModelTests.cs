using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.Bot.Assets;

namespace D3B.App.Tests.ViewModels;

public sealed class RoutesViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<RoutesViewModel> RoutesAsync()
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        return routes;
    }

    [Fact]
    public async Task ANewRouteIsAnEmptyFileInTheFolderChosenNamedFreshAndOpenedInItsTab()
    {
        var routes = await RoutesAsync();
        var opened = new List<string>();

        routes.Opened = editor => opened.Add(editor.Name);

        await _store.Assets.CreateFolderAsync("Farm", TestContext.Current.CancellationToken);

        var first = await routes.CreateAsync("Farm");
        var second = await routes.CreateAsync("");

        Assert.Equal(["New route", "New route 2"], opened);
        Assert.Equal("Farm", Path.GetDirectoryName(_store.Assets.Find(first!.Id)!.Path));
        Assert.Equal(AssetKind.Route, _store.Assets.Find(second!.Id)!.Kind);
        Assert.Equal([first, second], routes.Open);
        Assert.True(first!.IsEmpty);
    }

    [Fact]
    public async Task OpeningARouteTwiceKeepsOneEditorAndAnAssetOfAnotherKindOpensNone()
    {
        var routes = await RoutesAsync();
        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North" }, TestContext.Current.CancellationToken);
        var profile = _store.Assets.Assets.First(asset => asset.IsGiven);

        Assert.Same(routes.OpenRoute(route.Id), routes.OpenRoute(route.Id));
        Assert.Single(routes.Open);
        Assert.Null(routes.OpenRoute(profile.Id));
        Assert.Null(routes.OpenRoute("nobody"));
    }

    [Fact]
    public async Task ARouteDeletedFromTheProjectClosesItsEditorAndOneChangedTakesTheFileWhenNothingIsUnsaved()
    {
        var routes = await RoutesAsync();
        var ct = TestContext.Current.CancellationToken;
        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }] }, ct);
        var editor = routes.OpenRoute(route.Id)!;
        var closed = new List<string>();

        routes.Closed = item => closed.Add(item.Id);
        await _store.Assets.SaveAsync(route with { Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, ct);
        routes.Refresh(null);
        await editor.Planned;

        Assert.Equal([1L, 2L], editor.Steps.Select(step => step.MapId));
        Assert.False(editor.IsDirty);

        await _store.Assets.DeleteAsync(route.Id, ct);
        routes.Refresh(null);

        Assert.Equal(["r1"], closed);
        Assert.Empty(routes.Open);
    }

    [Fact]
    public async Task SavingEveryRouteWithChangesSavesThemAllAndTellsSo()
    {
        var routes = await RoutesAsync();
        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North" }, TestContext.Current.CancellationToken);
        var editor = routes.OpenRoute(route.Id)!;

        editor.TraceSquare(1, 0, isDrag: false);

        Assert.True(routes.IsAnyDirty);

        routes.SaveAll();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.False(routes.IsAnyDirty);
        Assert.Equal([1L], Assert.IsType<RouteAsset>(_store.Assets.Find("r1")).Anchors.Select(anchor => anchor.MapId));
        Assert.Contains(routes.Toasts.Recent, toast => toast.Text.Contains("North", StringComparison.Ordinal));
    }
}
