using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.World;
using D3B.App.ViewModels.Zones;
using D3B.Studio.Assets;

namespace D3B.App.Tests.ViewModels.World;

[UiThread]
public sealed class OutlinerViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private int _shown;

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<(OutlinerViewModel Outliner, WorldSelection Selection, WorldViewportViewModel World)> OutlinerAsync()
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        var zones = new ZonesViewModel(_localizer, new Dialogs(), new Toasts(), () => routes.Services);
        var selection = new WorldSelection();
        var world = new WorldViewportViewModel(_localizer, routes, zones, selection, () => [], () => null);
        var characters = new CharactersViewModel(_localizer, new BotsViewModel(_localizer), new CharacterAccounts(_localizer, new Dialogs(), new Toasts()), _ => { }, _ => { });
        var outliner = new OutlinerViewModel(_localizer, selection, world, characters, routes, zones) { Show = () => _shown++ };

        await _store.Assets.SaveAsync(new RouteAsset { Id = "south", Name = "South", Anchors = [new RouteAnchor { MapId = 3 }, new RouteAnchor { MapId = 4 }] }, TestContext.Current.CancellationToken);
        await _store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }, new RouteAnchor { MapId = 3 }] }, TestContext.Current.CancellationToken);
        await _store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forest", Maps = [1, 2] }, TestContext.Current.CancellationToken);
        world.Refresh();
        outliner.Refresh();

        return (outliner, selection, world);
    }

    [Fact]
    public async Task TheWorldIsListedInGroupsRoutesAndZonesByNameWithTheirSizes()
    {
        var (outliner, _, _) = await OutlinerAsync();

        Assert.Equal(["heading:routes", "route:north", "route:south", "heading:zones", "zone:forest"], outliner.Rows.Select(row => row.Key));
        Assert.Equal(["3 steps", "2 steps"], outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Route).Select(row => row.Detail));
        Assert.Equal("2 maps", outliner.Rows.Single(row => row.Kind is OutlinerRowKind.Zone).Detail);
        Assert.False(outliner.IsEmpty);
    }

    [Fact]
    public async Task ARouteUnfoldsIntoItsStepsAndSelectingAStepSelectsItInTheWorld()
    {
        var (outliner, selection, world) = await OutlinerAsync();
        var north = outliner.Rows.Single(row => row.Key == "route:north");

        outliner.ToggleExpanded(north);

        var steps = outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).ToList();

        Assert.Equal(["step:north:0", "step:north:1", "step:north:2"], steps.Select(row => row.Key));
        Assert.Equal("1,0", steps[0].Title);
        Assert.True(north.IsExpanded);

        outliner.Select(steps[1]);

        Assert.True(selection.IsRoute("north"));
        Assert.Equal(1, selection.Step);
        Assert.Equal(1, world.Route!.SelectedIndex);
        Assert.True(outliner.Rows.Single(row => row.Key == "step:north:1").IsSelected);
        Assert.False(outliner.Rows.Single(row => row.Key == "route:north").IsSelected);
        Assert.Equal(1, _shown);

        outliner.ToggleExpanded(outliner.Rows.Single(row => row.Key == "route:north"));

        Assert.DoesNotContain(outliner.Rows, row => row.Kind is OutlinerRowKind.Step);
    }

    [Fact]
    public async Task WhatIsSelectedElsewhereLightsItsRowAndAClosedEyeIsTheMapsNotTheProjects()
    {
        var (outliner, selection, world) = await OutlinerAsync();

        selection.SelectZone("forest");
        outliner.Refresh();

        Assert.True(outliner.Rows.Single(row => row.Key == "zone:forest").IsSelected);

        var south = outliner.Rows.Single(row => row.Key == "route:south");

        outliner.ToggleEye(south);

        Assert.False(south.IsEyeOpen);
        Assert.True(world.IsHidden("route:south"));
        Assert.NotNull(_store.Assets.Find("south"));

        outliner.ToggleEye(south);

        Assert.True(south.IsEyeOpen);
    }

    [Fact]
    public async Task TheFilterKeepsTheRowsThatMatchAndUnfoldsTheRoutesWhoseStepsDo()
    {
        var (outliner, _, _) = await OutlinerAsync();

        outliner.Filter = "sou";

        Assert.Equal(["heading:routes", "route:south"], outliner.Rows.Select(row => row.Key));

        // A route's step is found by its coordinates.
        outliner.Filter = "4,0";

        Assert.Equal(["heading:routes", "route:south", "step:south:1"], outliner.Rows.Select(row => row.Key));

        outliner.Filter = "nothing like this";

        Assert.True(outliner.IsEmpty);

        outliner.Filter = "";

        Assert.Equal(5, outliner.Rows.Count);
    }

    [Fact]
    public async Task TheRowsKeepTheirObjectsWhenTheWorldIsReadAgainSoThatAScrollIsNotLost()
    {
        var (outliner, _, _) = await OutlinerAsync();
        var before = outliner.Rows.ToList();

        await _store.Assets.SaveAsync(new RouteAsset { Id = "east", Name = "East", Anchors = [new RouteAnchor { MapId = 1 }] }, TestContext.Current.CancellationToken);
        outliner.Refresh();

        Assert.Equal(before.Count + 1, outliner.Rows.Count);
        Assert.All(before, row => Assert.Contains(row, outliner.Rows));
    }
}
