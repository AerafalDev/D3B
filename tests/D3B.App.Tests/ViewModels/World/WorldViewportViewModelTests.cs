using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.World;
using D3B.App.ViewModels.Zones;
using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels.World;

[UiThread]
public sealed class WorldViewportViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly List<WorldCharacterSnapshot> _characters = [];
    private readonly List<FollowedRun> _runs = [];
    private string? _framed;

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<(RoutesViewModel Routes, ZonesViewModel Zones, WorldSelection Selection, WorldViewportViewModel World)> WorldAsync(TestGameData? data = null)
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        var services = TestRouteWorld.Services(_store.Assets);

        routes.Use(data is null ? services : services with { Texts = data.Texts, Provider = new ServiceCollection().AddSingleton(data.Tables).BuildServiceProvider() });

        var zones = new ZonesViewModel(_localizer, new Dialogs(), new Toasts(), () => routes.Services);
        var selection = new WorldSelection();
        var world = new WorldViewportViewModel(_localizer, routes, zones, selection, () => _characters, () => _framed, new FollowedRuns(() => _runs));

        world.Refresh();

        return (routes, zones, selection, world);
    }

    private async Task<RouteAsset> SaveRouteAsync(string id, params long[] maps)
    {
        return (RouteAsset)await _store.Assets.SaveAsync(new RouteAsset { Id = id, Name = "Route " + id, Anchors = [.. maps.Select(map => new RouteAnchor { MapId = map })] }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheRouteSelectedTakesTheMapsGesturesAndTheOthersStayDrawnAroundIt()
    {
        var (_, _, selection, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2);
        await _store.Assets.SaveAsync(new RouteAsset
        {
            Id = "south",
            Name = "South",
            Anchors = [new RouteAnchor { MapId = 3, Actions = [new NodeCall(RouteActions.Harvest, System.Collections.Immutable.ImmutableSortedDictionary<string, System.Text.Json.Nodes.JsonNode?>.Empty)] }, new RouteAnchor { MapId = 4 }],
        }, TestContext.Current.CancellationToken);
        world.Refresh();

        Assert.Null(world.Route);
        Assert.Equal(["north", "south"], world.Scene.Backdrop!.Routes.Select(route => route.Id).Order());

        selection.SelectRoute("north");

        Assert.Equal("north", world.Route!.Id);
        Assert.Equal(2, world.Scene.Stations.Count);
        Assert.Equal(["south"], world.Scene.Backdrop!.Routes.Select(route => route.Id));
        Assert.Equal([WorldMapActionKind.Harvest], world.Scene.Backdrop.Routes[0].Steps[0].Actions);

        selection.SelectRoute("south");

        Assert.Equal("south", world.Route!.Id);
        Assert.Equal([WorldMapActionKind.Harvest], world.Scene.Stations[0].Actions);
        Assert.Equal(["north"], world.Scene.Backdrop!.Routes.Select(route => route.Id));

        selection.Clear();

        Assert.Null(world.Route);
        Assert.Equal(WorldMapTool.Select, world.MapTool);
    }

    [Fact]
    public async Task APressOnAStepOfAnotherRouteSelectsItAndTheStepInItsEditor()
    {
        var (_, _, selection, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2);
        await SaveRouteAsync("south", 3, 4);
        world.Refresh();
        world.Press(new WorldMapHit(WorldMapHitKind.RouteStep, 4, 0, -1, -1, "south", 1));

        Assert.True(selection.IsRoute("south"));
        Assert.Equal(1, selection.Step);
        Assert.Equal(1, world.Route!.SelectedIndex);

        // The step chosen in the editor, from the map's own station, is the world's selection.
        world.SelectStation(0);

        Assert.Equal(0, selection.Step);
    }

    [Fact]
    public async Task TheRouteToolMakesARouteOnItsFirstPressWhenNoneIsSelectedAndTracesIt()
    {
        var (routes, _, selection, world) = await WorldAsync();

        world.SetTool(WorldTool.Route);
        await world.Creating;

        Assert.Equal(WorldTool.Route, world.Tool);
        Assert.Equal(WorldMapTool.Trace, world.MapTool);
        Assert.True(selection.IsRoute(world.Route!.Id));
        Assert.Contains(routes.Open, editor => editor == world.Route);

        world.TraceSquare(1, 0, isDrag: false);
        world.TraceSquare(2, 0, isDrag: false);

        Assert.Equal(2, world.Scene.Stations.Count);
        Assert.True(world.Route.IsDirty);

        // Edit's Undo follows the editor the world's gestures go to.
        world.Route.Undo();

        Assert.Single(world.Scene.Stations);
    }

    [Fact]
    public async Task TracingOrPressingAStationOnTheMapNeverMovesTheMapUnderThePointer()
    {
        var (_, _, _, world) = await WorldAsync();
        var revealed = new List<(int X, int Y)>();

        world.SetTool(WorldTool.Route);
        await world.Creating;
        world.RevealRequested += (x, y) => revealed.Add((x, y));

        world.TraceSquare(1, 0, isDrag: false);
        world.TraceSquare(2, 0, isDrag: true);
        world.SelectStation(0);

        Assert.Empty(revealed);
    }

    [Fact]
    public async Task TheZoneToolPaintsTheZoneSelectedWithItsOwnToolsAndLeavesWhenSomethingElseIsSelected()
    {
        var (_, _, selection, world) = await WorldAsync();

        world.SetTool(WorldTool.Zone);
        await world.Creating;

        Assert.Equal(WorldTool.Zone, world.Tool);
        Assert.True(world.ShowsZoneTools);

        world.TraceSquare(1, 0, isDrag: false);
        world.SetZoneTool(WorldMapTool.Erase);

        Assert.Equal(WorldMapTool.Erase, world.MapTool);
        Assert.Equal(WorldMapTool.Erase, world.Zone!.Tool);
        Assert.Single(world.Scene.Squares!);

        await SaveRouteAsync("north", 1, 2);
        selection.SelectRoute("north");

        // A tool needs what it works on: the selection went to a route, so the zone tool is left.
        Assert.Equal(WorldTool.Select, world.Tool);
        Assert.Null(world.Zone);
        Assert.False(world.ShowsZoneTools);
    }

    [Fact]
    public async Task TheMapIsComposedOnOneThreadWhateverTheEditorsPlanningResumesOn()
    {
        var (_, _, selection, world) = await WorldAsync();
        var threads = new System.Collections.Concurrent.ConcurrentBag<int>();

        world.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorldViewportViewModel.Scene))
                threads.Add(Environment.CurrentManagedThreadId);
        };

        await SaveRouteAsync("north", 1, 2);
        await SaveRouteAsync("south", 3, 4);

        for (var i = 0; i < 30; i++)
        {
            await _store.Assets.SaveAsync(new ZoneAsset { Id = "zone" + i, Name = "Zone " + i, Maps = [1, 2, 3] }, TestContext.Current.CancellationToken);
        }

        world.Refresh();

        // Each editor opened plans its route off the thread and then redraws what it holds: that redraw is the map's, so it must come back.
        selection.SelectRoute("north");
        selection.SelectRoute("south");
        await world.Route!.Planned;
        selection.SelectRoute("north");
        await world.Route!.Planned;

        Assert.Single(threads.Distinct());
    }

    [Fact]
    public async Task AClosedEyeLeavesARouteAZoneOrACharacterOffTheMapAndTheEditorOfWhatIsSelectedKeepsItsOwnDrawing()
    {
        var (_, _, selection, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2);
        await SaveRouteAsync("south", 3, 4);
        await _store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forest", Maps = [1, 2] }, TestContext.Current.CancellationToken);
        world.Refresh();

        Assert.Equal(2, world.Scene.Backdrop!.Routes.Count);
        Assert.Equal(["forest"], world.Scene.Backdrop.Zones.Select(zone => zone.Id));

        world.ToggleVisible("route:south");
        world.ToggleVisible("zone:forest");

        Assert.Equal(["north"], world.Scene.Backdrop!.Routes.Select(route => route.Id));
        Assert.Empty(world.Scene.Backdrop.Zones);
        Assert.True(world.IsHidden("route:south"));

        world.ToggleVisible("route:south");
        selection.SelectRoute("north");

        Assert.Equal(["south"], world.Scene.Backdrop!.Routes.Select(route => route.Id));
    }

    [Fact]
    public async Task SelectingARouteInAnotherWorldShowsThatWorldAndTheOtherRoutesAreDrawnInTheLayerShownOnly()
    {
        var (_, _, selection, world) = await WorldAsync();

        await SaveRouteAsync("elsewhere", 7);
        await SaveRouteAsync("north", 1, 2);
        world.Refresh();

        // World 1 first: the route in world 2 has no step to draw there.
        Assert.Equal(1, world.Layer);
        Assert.Equal(["north"], world.Scene.Backdrop!.Routes.Select(route => route.Id));

        selection.SelectRoute("elsewhere");

        Assert.Equal(2, world.Layer);
        Assert.Empty(world.Scene.Backdrop!.Routes);

        world.UseLayer(1);

        Assert.Equal(1, world.Route!.Layer);
        Assert.Equal(["north"], world.Scene.Backdrop!.Routes.Select(route => route.Id));
    }

    [Fact]
    public async Task ACharacterIsDrawnOnItsMapAndSelectingItShowsItsWorldAndPutsTheMapOnIt()
    {
        var (_, _, selection, world) = await WorldAsync();
        var here = new D3B.Bot.Characters.CharacterKey(1, 2);
        var there = new D3B.Bot.Characters.CharacterKey(1, 3);
        var blue = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Colors.Blue);

        _characters.Add(new WorldCharacterSnapshot(here, "Ayuwoki", "AY", blue, 2, true));
        _characters.Add(new WorldCharacterSnapshot(there, "Nolwenn", "NO", blue, 7, false));
        world.Refresh();

        var drawn = Assert.Single(world.Scene.Backdrop!.Characters);

        Assert.Equal(("Ayuwoki", 2, 0, true), (drawn.Name, drawn.X, drawn.Y, drawn.IsPlaying));

        var revealed = new List<(int X, int Y)>();

        world.RevealRequested += (x, y) => revealed.Add((x, y));
        selection.SelectCharacter(there);

        Assert.Equal(2, world.Layer);
        Assert.Equal([(1, 1)], revealed);
        Assert.Equal(["Nolwenn"], world.Scene.Backdrop!.Characters.Select(character => character.Name));
        Assert.True(world.Scene.Backdrop.Characters[0].IsSelected);

        // Pressed on the map, a character is selected where it stands: the map is not moved.
        revealed.Clear();
        world.UseLayer(1);
        world.Press(new WorldMapHit(WorldMapHitKind.Character, 2, 0, -1, -1, "1:2"));

        Assert.True(selection.IsCharacter(here));
        Assert.Empty(revealed);
    }

    [Fact]
    public async Task TheFrameIsGreenAndNamesTheCharacterWhileItPlays()
    {
        var (_, _, _, world) = await WorldAsync();

        Assert.False(world.IsFramed);

        _framed = "Ayuwoki";
        world.Refresh();

        Assert.True(world.IsFramed);
        Assert.Equal("Ayuwoki plays", world.FrameText);

        _framed = null;
        world.Refresh();

        Assert.False(world.IsFramed);
    }

    private static FollowedRun Following(string name, int anchor, bool arrived, RouteMode mode = RouteMode.OneWay, bool returning = false, long laps = 0)
    {
        var position = new RouteRunPosition(new CharacterKey(1, 2), "north", "North", mode, 3, anchor, arrived, 0, 0, laps, returning);

        return new FollowedRun(position, name, name[..2].ToUpperInvariant(), null);
    }

    [Fact]
    public async Task TheStepACharacterGoesToIsMarkedOnTheMapWithTheLegItWalksUntilItArrives()
    {
        var (_, _, _, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2, 3);
        _runs.Add(Following("Ayuwoki", 1, false));
        world.Refresh();

        var steps = world.Scene.Backdrop!.Routes.Single().Steps;
        var run = Assert.Single(world.Scene.Backdrop.Runs);

        Assert.Equal((steps[1].X, steps[1].Y, steps[0].X, steps[0].Y, false, "AY"), (run.X, run.Y, run.FromX, run.FromY, run.Arrived, run.Initials));

        _runs[0] = Following("Ayuwoki", 1, true);
        world.Refresh();

        Assert.True(Assert.Single(world.Scene.Backdrop.Runs).Arrived);

        _runs.Clear();
        world.Refresh();

        Assert.Empty(world.Scene.Backdrop.Runs);
    }

    [Fact]
    public async Task ARouteHiddenByItsEyeLeavesItsRunsOffTheMapAndTheFirstStepOfAOneWayRouteHasNoLeg()
    {
        var (_, _, _, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2, 3);
        _runs.Add(Following("Ayuwoki", 0, false));
        world.Refresh();

        Assert.Null(Assert.Single(world.Scene.Backdrop!.Runs).FromX);

        world.ToggleVisible("route:north");

        Assert.Empty(world.Scene.Backdrop!.Runs);
    }

    [Fact]
    public async Task TheLegOfALoopsFirstStepComesFromItsLastAndTheLegOfARouteGoingBackComesFromTheStepAfter()
    {
        var (_, _, _, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2, 3);
        _runs.Add(Following("Ayuwoki", 0, false, RouteMode.Loop));
        world.Refresh();

        var steps = world.Scene.Backdrop!.Routes.Single().Steps;
        var loop = Assert.Single(world.Scene.Backdrop.Runs);

        Assert.Equal((steps[2].X, steps[2].Y), (loop.FromX, loop.FromY));

        _runs[0] = Following("Ayuwoki", 1, false, RouteMode.RoundTrip, returning: true);
        world.Refresh();

        var back = Assert.Single(world.Scene.Backdrop.Runs);

        Assert.Equal((steps[2].X, steps[2].Y), (back.FromX, back.FromY));
    }

    [Fact]
    public async Task TheStepOfTheRouteSelectedThatTheSequencerHoversIsLitOnTheMap()
    {
        var (_, _, selection, world) = await WorldAsync();

        await SaveRouteAsync("north", 1, 2, 3);
        selection.SelectRoute("north");

        Assert.Equal(-1, world.Scene.LitStep);

        world.Route!.Sequencer.Hover(1);

        Assert.Equal(1, world.Scene.LitStep);

        world.Route.Sequencer.Hover(-1);

        Assert.Equal(-1, world.Scene.LitStep);

        world.Route.Sequencer.Hover(2);
        selection.SelectRoute("north", 0);
        selection.Clear();

        Assert.Equal(-1, world.Scene.LitStep);
    }

    [Fact]
    public async Task TheFrameSaysTheStepAndTheLapOfTheRouteTheCharacterFollows()
    {
        var (_, _, _, world) = await WorldAsync();

        _framed = "Ayuwoki";
        _runs.Add(Following("Ayuwoki", 1, true, RouteMode.Loop, laps: 2));
        world.Refresh();

        Assert.Equal("Ayuwoki plays · step 2/3 · lap 3", world.FrameText);

        _runs.Clear();
        world.Refresh();

        Assert.Equal("Ayuwoki plays", world.FrameText);
    }

    [Fact]
    public async Task TheHintsOfTheWorldShownFollowItsLayerAndTheirHoverSaysWhatTheyAre()
    {
        using var data = new TestGameData();

        var (_, _, _, world) = await WorldAsync(data);

        Assert.Equal([1, 2, 3], world.Hints.Visible.Select(hint => hint.Id).Order());

        world.Hover(new WorldMapHit(WorldMapHitKind.Hint, 2, 0, -1, -1, "2"));

        Assert.Equal("White Rat Larder · Dungeons · level 110 · 2,0", world.Hud);

        world.UseLayer(2);

        Assert.Equal([4], world.Hints.Visible.Select(hint => hint.Id));

        world.Hover(new WorldMapHit(WorldMapHitKind.Hint, 1, 1, -1, -1, "4"));

        Assert.Equal("Boat to Amakna · Transports · 1,1", world.Hud);
    }

    [Fact]
    public async Task WithoutHintsInThePackTheMapDrawsNoneAndTheMenuOffersNone()
    {
        var (_, _, _, world) = await WorldAsync();

        Assert.False(world.Hints.IsAvailable);
        Assert.Empty(world.Hints.Visible);
    }

    [Fact]
    public void CharactersOnTheSameMapAreDrawnBesideEachOtherAndNotOneOverTheOther()
    {
        WorldMapCharacter Character(string key, int x)
        {
            return new WorldMapCharacter(key, key, "XX", Avalonia.Media.Colors.Gray, x, 0, false, false);
        }

        var centers = WorldMapBackdropLayer.CharacterCenters([Character("a", 1), Character("b", 1), Character("c", 2)], (x, y) => new Avalonia.Rect(x * 64, y * 46, 64, 46));

        Assert.NotEqual(centers[0], centers[1]);
        Assert.Equal(centers[0].Y, centers[1].Y);
        Assert.True(WorldMapBackdropLayer.HitsCharacter(centers[1], centers[1]));
        Assert.False(WorldMapBackdropLayer.HitsCharacter(centers[0], centers[1]));
    }
}
