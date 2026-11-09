using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.World;
using D3B.App.ViewModels.Zones;
using D3B.App.Views;
using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.Views;

public sealed class WorldViewTests
{
    // The language is the window's one, shared by every test of the session: it is set, and read, on the interface's thread alone.
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WorldViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WorldViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static NodeCall Harvest()
    {
        return new NodeCall(RouteActions.Harvest, System.Collections.Immutable.ImmutableSortedDictionary<string, System.Text.Json.Nodes.JsonNode?>.Empty);
    }

    private static NodeCall Fight()
    {
        return new NodeCall(RouteActions.Fight, System.Collections.Immutable.ImmutableSortedDictionary<string, System.Text.Json.Nodes.JsonNode?>.Empty);
    }

    [Fact]
    public async Task TheKeysChooseTheToolOfWhatIsSelectedInTheWorldTab()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var result = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets));

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            main.ShowWorld();
            Dispatcher.UIThread.RunJobs();

            var kept = (main.Editor.Items.Count, main.Editor.Selected?.Key, main.Editor.Selected?.IsPinned);
            var view = window.GetVisualDescendants().OfType<WorldView>().Single(item => item.IsEffectivelyVisible);
            var map = view.GetVisualDescendants().OfType<WorldMapView>().Single();

            // B with nothing selected makes a route and traces it, as the Route tool's button does.
            map.Focus();
            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.None);
            await main.World.Creating;
            Dispatcher.UIThread.RunJobs();

            var route = main.World.Route!;
            var tracing = (main.World.Tool, map.Tool, main.Selection.IsRoute(route.Id));

            main.World.TraceSquare(1, 0, isDrag: false);
            main.World.TraceSquare(2, 0, isDrag: false);
            Dispatcher.UIThread.RunJobs();

            var stations = map.Scene.Stations.Count;

            window.KeyPressQwerty(PhysicalKey.V, RawInputModifiers.None);

            var selecting = (main.World.Tool, map.Tool, route.Tool);

            // Edit's Undo takes back what the route selected did.
            main.Undo();

            var undone = (map.Scene.Stations.Count, main.UndoCommand.CanExecute(null), main.RedoCommand.CanExecute(null));

            // A zone made from the assets is selected in the same map, and the keys reach its tools.
            var zone = (await main.Zones.CreateAsync(""))!;

            Dispatcher.UIThread.RunJobs();

            map.Focus();
            window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);

            var erasing = (main.World.Tool, map.Tool, zone.Tool);

            window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);

            var subArea = (main.World.MapTool, zone.Tool);

            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.None);

            var painting = (main.World.Tool, main.World.MapTool);

            window.Close();

            return (kept, tracing, stations, selecting, undone, erasing, subArea, painting, main.Editor.Items.Count);
        });

        Assert.Equal((2, "world", false), result.kept);
        Assert.Equal((WorldTool.Route, WorldMapTool.Trace, true), result.tracing);
        Assert.Equal(2, result.stations);
        Assert.Equal((WorldTool.Select, WorldMapTool.Select, WorldMapTool.Select), result.selecting);
        Assert.Equal((1, true, true), result.undone);
        Assert.Equal((WorldTool.Zone, WorldMapTool.Erase, WorldMapTool.Erase), result.erasing);
        Assert.Equal((WorldMapTool.Region, WorldMapTool.Region), result.subArea);
        Assert.Equal((WorldTool.Zone, WorldMapTool.Trace), result.painting);

        // No tab for the route nor the zone: the home page and the world are the only two.
        Assert.Equal(2, result.Item9);
    }

    [Fact]
    public async Task TheWorldShowsItsRoutesZonesAndCharactersAndTheFrameIsGreenWhileACharacterPlays()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;

        await store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "Bûcheron d'Astrub", Mode = RouteMode.Loop, Anchors = [new RouteAnchor { MapId = 1, Actions = [Harvest()] }, new RouteAnchor { MapId = 2, Actions = [Harvest(), Fight()] }, new RouteAnchor { MapId = 3 }] }, ct);
        await store.Assets.SaveAsync(new RouteAsset { Id = "south", Name = "Sud", Anchors = [new RouteAnchor { MapId = 4, Actions = [Fight()] }, new RouteAnchor { MapId = 5 }] }, ct);
        await store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forêt d'Astrub", Maps = [2, 3, 4] }, ct);

        var result = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var routes = new RoutesViewModel(localizer, new Dialogs(), new Toasts());

            routes.Use(TestRouteWorld.Services(store.Assets));

            var zones = new ZonesViewModel(localizer, new Dialogs(), new Toasts(), () => routes.Services);
            var selection = new WorldSelection();
            var blue = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#3B82F6"));
            var pink = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#E2549A"));
            string? playing = "Ayuwoki";
            var world = new WorldViewportViewModel(
                localizer,
                routes,
                zones,
                selection,
                () => [new WorldCharacterSnapshot(new CharacterKey(1, 1), "Ayuwoki", "AY", blue, 2, true), new WorldCharacterSnapshot(new CharacterKey(1, 2), "Nolwenn", "NO", pink, 4, false)],
                () => playing);

            world.Refresh();

            var window = new Window { Content = new WorldView { DataContext = world }, Width = 1180, Height = 720 };

            window.Show();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "world-backdrop");

            selection.SelectRoute("north", 1);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "world-route-selected");

            selection.SelectZone("forest");
            world.SetTool(WorldTool.Zone);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "world-zone-selected");

            // Escape leaves the zone tool, whichever tool is in use.
            window.GetVisualDescendants().OfType<WorldMapView>().Single().Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

            var escaped = world.Tool;
            var frame = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PlayFrame");
            var framed = (frame.IsVisible, world.FrameText);

            playing = null;
            world.Refresh();
            Dispatcher.UIThread.RunJobs();

            var unframed = frame.IsVisible;
            var characters = world.Scene.Backdrop!.Characters.Select(character => character.Name).ToList();

            window.Close();

            return (framed, unframed, characters, escaped);
        });

        Assert.Equal((true, "Ayuwoki plays"), result.framed);
        Assert.False(result.unframed);
        Assert.Equal(["Ayuwoki", "Nolwenn"], result.characters);
        Assert.Equal(WorldTool.Select, result.escaped);
    }

    [Fact]
    public async Task TheMapDrawsTheHintsThatTheMenusTicksChooseAndHoveringOneSaysWhatItIs()
    {
        await using var store = new TestStore();
        using var data = new TestGameData();

        await store.OpenedAsync();

        var result = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var routes = new RoutesViewModel(localizer, new Dialogs(), new Toasts());

            routes.Use(TestRouteWorld.Services(store.Assets) with { Texts = data.Texts, Provider = new ServiceCollection().AddSingleton(data.Tables).BuildServiceProvider() });

            var zones = new ZonesViewModel(localizer, new Dialogs(), new Toasts(), () => routes.Services);
            var world = new WorldViewportViewModel(localizer, routes, zones, new WorldSelection(), () => [], () => null);

            world.Refresh();

            var window = new Window { Content = new WorldView { DataContext = world }, Width = 1180, Height = 720 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var map = window.GetVisualDescendants().OfType<WorldMapView>().Single();
            var all = map.Hints.Select(hint => hint.Id).Order().ToList();

            Drawings.Draw(window, "world-hints");
            world.Hints.Categories.Single(category => category.Id == 9).IsOn = false;
            Dispatcher.UIThread.RunJobs();

            var unticked = map.Hints.Select(hint => hint.Id).Order().ToList();

            world.Hints.Categories.Single(category => category.Id == 9).IsOn = true;
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(map.TranslatePoint(map.RectOf(3, 0).Center, window)!.Value);
            Dispatcher.UIThread.RunJobs();

            var hovered = (map.Hit.Kind, world.Hud);

            window.Close();

            return (all, unticked, hovered);
        });

        Assert.Equal([1, 2, 3], result.all);
        Assert.Equal([2, 3], result.unticked);
        Assert.Equal((WorldMapHitKind.Hint, "Zaap · Transports · 3,0"), result.hovered);
    }

    [Fact]
    public async Task TheStudioShowsTheOutlinerTheWorldAndTheDetailsOfWhatIsSelected()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;

        await store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "Bûcheron d'Astrub", Mode = RouteMode.Loop, Anchors = [new RouteAnchor { MapId = 1, Actions = [Harvest()] }, new RouteAnchor { MapId = 2, Actions = [Harvest(), Fight()] }, new RouteAnchor { MapId = 3 }] }, ct);
        await store.Assets.SaveAsync(new RouteAsset { Id = "south", Name = "Sud", Anchors = [new RouteAnchor { MapId = 4, Actions = [Fight()] }, new RouteAnchor { MapId = 5 }] }, ct);
        await store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forêt d'Astrub", Maps = [2, 3, 4] }, ct);
        await store.Play.EnterAsync(300);

        var runner = await TestHosts.RunningAsync(store.Host);
        var result = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets));
            main.Refresh();

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-engine-nothing");

            var outliner = window.GetVisualDescendants().OfType<OutlinerView>().Single();
            var north = main.Outliner.Rows.Single(row => row.Key == "route:north");

            main.Outliner.ToggleExpanded(north);
            main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "step:north:1"));
            Dispatcher.UIThread.RunJobs();
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-engine-step");

            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).OfType<string>().ToList();
            var route = (main.World.Route?.Id, main.Selection.Step, main.Properties.ShowsAsset, main.Properties.Asset.HasRoute);

            main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "zone:forest"));
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-engine-zone");

            var zone = (main.World.Zone?.Id, main.Properties.Asset.HasZone);

            main.Characters.Choose(main.Characters.Online.Single().Bot.Key);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-engine-character");

            var character = (main.Selection.Kind, main.Properties.ShowsCharacter, main.World.Route is null, main.World.Zone is null);
            var rows = main.Outliner.Rows.Select(row => row.Key).ToList();

            window.Close();

            return (texts, route, zone, character, rows, outliner.IsEffectivelyVisible);
        });

        Assert.Contains("Outliner", result.texts);
        Assert.Contains("Details", result.texts);
        Assert.Contains("World", result.texts);
        Assert.Contains("Select", result.texts);
        Assert.Contains("Bûcheron d'Astrub", result.texts);
        Assert.Equal(("north", 1, true, true), result.route);
        Assert.Equal(("forest", true), result.zone);
        Assert.Equal((WorldSelectionKind.Character, true, true, true), result.character);
        Assert.Contains("route:north", result.rows);
        Assert.Contains(result.rows, key => key.StartsWith("character:", StringComparison.Ordinal));
        Assert.True(result.Item6);
    }
}
