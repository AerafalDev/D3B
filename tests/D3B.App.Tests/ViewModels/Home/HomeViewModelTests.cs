using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.World;
using D3B.Bot.Tests.Runtime;
using D3B.Studio.Assets;

namespace D3B.App.Tests.ViewModels.Home;

public sealed class HomeViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<MainViewModel> StudioAsync()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;

        await _store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, ct);
        await _store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forest", Maps = [1, 2] }, ct);

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer);

        main.Routes.Use(TestRouteWorld.Services(_store.Assets));
        main.Refresh();

        return main;
    }

    [Fact]
    public async Task TheStudioOpensOnTheHomePageAndTheWorldIsNotOpenYet()
    {
        var main = await StudioAsync();

        Assert.Equal(["home"], main.Editor.Items.Select(tab => tab.Key));
        Assert.True(main.Editor.IsShown("home"));
        Assert.Equal("Home", main.Editor.Selected?.Title);
        Assert.Same(main, main.Editor.Selected?.Content);
    }

    [Fact]
    public async Task TheHomePageClosesLikeTheOthersAndTheWindowMenuOpensItAgainNamedInTheAppsLanguage()
    {
        var main = await StudioAsync();

        main.ShowSettings();
        main.Editor.Close(main.Editor.Items.Single(tab => tab.Key == "home"));

        Assert.DoesNotContain(main.Editor.Items, tab => tab.Key == "home");

        main.Editor.CloseAll();

        Assert.Empty(main.Editor.Items);

        main.Settings.UseFrenchCommand.Execute(null);
        main.ShowHomeCommand.Execute(null);

        Assert.Equal(["home"], main.Editor.Items.Select(tab => tab.Key));
        Assert.True(main.Editor.IsShown("home"));
        Assert.Equal("Accueil", main.Editor.Items.Single().Title);
        Assert.False(main.Editor.Items.Single().IsPinned);

        main.Settings.UseEnglishCommand.Execute(null);
    }

    [Fact]
    public async Task SelectingARouteOrAZoneInTheOutlinerShowsTheWorldInOneTab()
    {
        var main = await StudioAsync();

        main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "route:north"));

        Assert.True(main.Editor.IsShown("world"));
        Assert.True(main.Selection.IsRoute("north"));

        main.ShowHomeCommand.Execute(null);

        Assert.False(main.Editor.IsShown("world"));

        main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "zone:forest"));

        Assert.True(main.Editor.IsShown("world"));
        Assert.Equal(["home", "world"], main.Editor.Items.Select(tab => tab.Key));
    }

    [Fact]
    public async Task OpeningARouteOrAZoneFromAnywhereShowsTheWorld()
    {
        var main = await StudioAsync();

        main.OpenAsset("north");

        Assert.True(main.Editor.IsShown("world"));

        main.ShowHomeCommand.Execute(null);
        main.OpenAsset("forest");

        Assert.True(main.Editor.IsShown("world"));
        Assert.True(main.Selection.IsZone("forest"));
    }

    [Fact]
    public async Task TheWorldTabCanBeClosedAndComesBackWithTheNextSelection()
    {
        var main = await StudioAsync();

        main.OpenAsset("north");
        main.Editor.Close(main.Editor.Items.Single(tab => tab.Key == "world"));

        Assert.True(main.Editor.IsShown("home"));
        Assert.Equal(["home"], main.Editor.Items.Select(tab => tab.Key));

        main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "route:north"));

        Assert.True(main.Editor.IsShown("world"));
    }

    [Fact]
    public async Task TheHomePageListsTheProjectsAssetsLastChangedFirst()
    {
        var main = await StudioAsync();
        var library = _store.Assets;

        File.SetLastWriteTimeUtc(library.FileOf(library.Find("north")!)!, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(library.FileOf(library.Find("forest")!)!, DateTime.UtcNow.AddMinutes(-5));
        await library.ReloadAsync(TestContext.Current.CancellationToken);
        main.Refresh();

        Assert.Equal(["Forest", "North"], main.Home.Recents.Select(recent => recent.Name));
        Assert.Equal(["Zone", "Route"], main.Home.Recents.Select(recent => recent.Kind));
        Assert.Equal("5 min ago", main.Home.Recents[0].When);
        Assert.True(main.Home.HasRecents);
    }

    [Fact]
    public async Task OpeningARecentRouteShowsTheWorldAndSelectsIt()
    {
        var main = await StudioAsync();
        var north = main.Home.Recents.Single(recent => recent.Name == "North");

        north.OpenCommand.Execute(null);

        Assert.True(main.Editor.IsShown("world"));
        Assert.True(main.Selection.IsRoute("north"));
    }

    [Fact]
    public async Task TheHomePageListsTheCharactersAndChoosingOneKeepsTheHomePage()
    {
        await _store.OpenedAsync();
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer);

        main.Refresh();

        var character = Assert.Single(main.Home.Characters);

        Assert.Equal(("Aerafa", "Disconnected"), (character.Name, character.Detail));
        Assert.True(main.Home.HasCharacters);

        character.ChooseCommand.Execute(null);

        Assert.Equal(TestPlay.Key, main.RunBlock.ChosenKey);
        Assert.True(main.Editor.IsShown("home"));
    }

    [Fact]
    public async Task ACharacterSelectedInTheOutlinerDoesNotLeaveTheHomePage()
    {
        await _store.OpenedAsync();
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer);

        main.Refresh();
        main.Outliner.Select(main.Outliner.Rows.Single(row => row.Kind is OutlinerRowKind.Character));

        Assert.True(main.Editor.IsShown("home"));
        Assert.Equal(TestPlay.Key, main.RunBlock.ChosenKey);
    }

    [Fact]
    public async Task TheQuickActionsMakeARouteAndAZoneAndShowTheWorldOrAskForAProfile()
    {
        var main = await StudioAsync();

        await main.Home.NewRouteCommand.ExecuteAsync(null);

        Assert.True(main.Editor.IsShown("world"));
        Assert.Equal(WorldSelectionKind.Route, main.Selection.Kind);

        main.ShowHomeCommand.Execute(null);
        await main.Home.NewZoneCommand.ExecuteAsync(null);

        Assert.True(main.Editor.IsShown("world"));
        Assert.Equal(WorldSelectionKind.Zone, main.Selection.Kind);

        main.ShowHomeCommand.Execute(null);
        main.Home.NewProfileCommand.Execute(null);

        Assert.True(main.Dialogs.IsOpen);
        Assert.True(main.Editor.IsShown("home"));
    }

    [Fact]
    public async Task TheSettingsQuickActionOpensTheSettings()
    {
        var main = await StudioAsync();

        main.Home.SettingsCommand.Execute(null);

        Assert.True(main.Editor.IsShown("settings"));
    }
}
