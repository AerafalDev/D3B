using Avalonia.Input;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.World;
using D3B.App.ViewModels.Zones;
using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

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

    private async Task<(OutlinerViewModel Outliner, WorldSelection Selection, WorldViewportViewModel World)> OutlinerAsync(FollowedRuns? followed = null)
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        var zones = new ZonesViewModel(_localizer, new Dialogs(), new Toasts(), () => routes.Services);
        var selection = new WorldSelection();
        var world = new WorldViewportViewModel(_localizer, routes, zones, selection, () => [], () => null);
        var characters = new CharactersViewModel(_localizer, new BotsViewModel(_localizer), new CharacterAccounts(_localizer, new Dialogs(), new Toasts()), _ => { }, _ => { });
        var outliner = new OutlinerViewModel(_localizer, selection, world, characters, routes, zones, followed) { Show = () => _shown++ };

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

    [Fact]
    public async Task ControlClickingStepsOfARouteSelectsSeveralAndShiftClickingSelectsThoseBetween()
    {
        var (outliner, selection, world) = await OutlinerAsync();

        outliner.ToggleExpanded(outliner.Rows.Single(row => row.Key == "route:north"));

        var steps = outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).ToList();

        outliner.Select(steps[0]);
        outliner.Select(steps[2], KeyModifiers.Control);

        Assert.Equal([0, 2], selection.Steps);
        Assert.Equal([0, 2], world.Route!.SelectedSteps);
        Assert.Equal([true, false, true], outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).Select(row => row.IsSelected));

        outliner.Select(steps[1]);
        outliner.Select(steps[2], KeyModifiers.Shift);

        Assert.Equal([1, 2], selection.Steps);
    }

    [Fact]
    public async Task ControlClickingAStepOfAnotherRouteSelectsItAlone()
    {
        var (outliner, selection, _) = await OutlinerAsync();

        outliner.ToggleExpanded(outliner.Rows.Single(row => row.Key == "route:north"));
        outliner.ToggleExpanded(outliner.Rows.Single(row => row.Key == "route:south"));
        outliner.Select(outliner.Rows.Single(row => row.Key == "step:north:0"));
        outliner.Select(outliner.Rows.Single(row => row.Key == "step:south:1"), KeyModifiers.Control);

        Assert.True(selection.IsRoute("south"));
        Assert.Equal([1], selection.Steps);
    }

    private static FollowedRun Following(string initials, string route, int anchor, bool arrived, int anchors = 3)
    {
        var position = new RouteRunPosition(new CharacterKey(1, anchor), route, route, RouteMode.OneWay, anchors, anchor, arrived, 0, 0, 0, false);

        return new FollowedRun(position, initials + " name", initials, null);
    }

    [Fact]
    public async Task ARouteFollowedSaysWhoIsOnItAndHowFarOnItsRowAndMarksTheStepEachIsAt()
    {
        var runs = new List<FollowedRun> { Following("AY", "north", 1, true), Following("BO", "north", 2, false) };
        var (outliner, _, _) = await OutlinerAsync(new FollowedRuns(() => runs));
        var north = outliner.Rows.Single(row => row.Key == "route:north");

        outliner.ToggleExpanded(north);

        Assert.Equal("3 steps · AY name 2/3, BO name 3/3", north.Detail);
        Assert.Equal("AY BO", north.Followers);
        Assert.Equal("", outliner.Rows.Single(row => row.Key == "route:south").Followers);
        Assert.Equal(["", "AY", "BO"], outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).Select(row => row.Followers));
        Assert.Equal([false, true, false], outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).Select(row => row.IsArrived));
    }

    [Fact]
    public async Task TheMarksFollowTheRunAsItMovesAndGoWhenItEnds()
    {
        var runs = new List<FollowedRun> { Following("AY", "north", 0, false) };
        var (outliner, _, _) = await OutlinerAsync(new FollowedRuns(() => runs));

        outliner.ToggleExpanded(outliner.Rows.Single(row => row.Key == "route:north"));
        runs[0] = Following("AY", "north", 1, true);
        outliner.Refresh();

        Assert.Equal(["", "AY", ""], outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step).Select(row => row.Followers));

        runs.Clear();
        outliner.Refresh();

        Assert.All(outliner.Rows.Where(row => row.Kind is OutlinerRowKind.Step), row => Assert.Equal("", row.Followers));
        Assert.Equal("3 steps", outliner.Rows.Single(row => row.Key == "route:north").Detail);
    }
}
