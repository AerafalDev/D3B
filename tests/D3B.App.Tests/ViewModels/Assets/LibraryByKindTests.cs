using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Assets;
using D3B.Bot.Tests.Settings;
using D3B.Data.Tests;
using D3B.Studio.Assets;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.ViewModels.Assets;

[UiThread]
public sealed class LibraryByKindTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static string Shape(AssetsViewModel assets)
    {
        return string.Join(" | ", assets.Rows.Select(row => new string(' ', row.Depth) + row.Kind switch
        {
            AssetRowKind.Folder => (row.IsExpanded ? "v " : "> ") + row.Name,
            AssetRowKind.Heading => (row.IsExpanded ? "# " : "+ ") + row.Name,
            AssetRowKind.Problem => "! " + row.Name,
            _ => row.Name,
        }));
    }

    private async Task SaveProjectAsync()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Walks = 7 }), ct);

        await _store.Assets.CreateFolderAsync("Farm", ct);
        await _store.Assets.MoveAsync(walker.Id, "Farm", ct);
        await _store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }] }, ct);
        await _store.Assets.SaveAsync(new ZoneAsset { Id = "forest", Name = "Forest", Maps = [1] }, ct);
        await File.WriteAllTextAsync(Path.Combine(_store.Assets.Folder, "Broken.profile.json"), "{", ct);
        await _store.Assets.LoadAsync([.. _store.Assets.Assets.Where(asset => asset.IsGiven)], ct);
    }

    private async Task<AssetsViewModel> LibraryAsync()
    {
        await SaveProjectAsync();

        var profiles = new ProfilesViewModel(_localizer, new Dialogs(), new Toasts());
        var assets = new AssetsViewModel(_localizer, profiles.Dialogs, profiles.Toasts, profiles);

        profiles.Refresh(_store.Host.Services);
        assets.Refresh(_store.Host.Services);

        return assets;
    }

    [Fact]
    public async Task TheLibraryListsTheProjectByKindFirstThenTheFilesThatDoNotReadThenTheGivenOnes()
    {
        var assets = await LibraryAsync();

        Assert.True(assets.IsByKind);
        Assert.StartsWith("# Profiles |  Walker | # Routes |  North | # Zones |  Forest | # Files that do not read |  ! Broken.profile.json | # Given with D3B", Shape(assets), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKindsHeadingFoldsAndUnfoldsItsAssets()
    {
        var assets = await LibraryAsync();
        var routes = assets.Rows.Single(row => row is { IsHeading: true, Name: "Routes" });

        assets.Toggle(routes);

        Assert.Contains("+ Routes | # Zones", Shape(assets), StringComparison.Ordinal);
        Assert.DoesNotContain(assets.Rows, row => row.Name == "North");

        assets.OpenRow(assets.Rows.Single(row => row is { IsHeading: true, Name: "Routes" }));

        Assert.Contains(assets.Rows, row => row.Name == "North");
    }

    [Fact]
    public async Task TheFoldersComeBackOnDemandAndTheChoiceIsRemembered()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");

        await SaveProjectAsync();

        var runner = await TestHosts.RunningAsync(_store.Host);
        var main = new MainViewModel(runner, _localizer, AppPreferences.Load(path));

        main.Refresh();
        main.Assets.ToggleByKindCommand.Execute(null);

        Assert.False(main.Assets.IsByKind);
        Assert.StartsWith("v Farm |  Walker", Shape(main.Assets), StringComparison.Ordinal);

        var again = new MainViewModel(runner, _localizer, AppPreferences.Load(path));

        again.Refresh();

        Assert.False(again.Assets.IsByKind);
    }

    [Fact]
    public async Task TheKeyboardGoesThroughTheHeadingsFoldsOneAndGoesBackToItsHeading()
    {
        var assets = await LibraryAsync();

        assets.MoveFocus(1);

        Assert.Equal("group:kind:Profile", assets.FocusKey);

        assets.MoveFocus(1);
        assets.MoveFocus(1);
        assets.MoveFocus(1);

        // Down goes from row to row, headings included, without folding any.
        Assert.Equal("north", assets.FocusKey);
        Assert.DoesNotContain(assets.Rows, row => row is { IsHeading: true, IsExpanded: false });

        assets.Collapse();

        Assert.Equal("group:kind:Route", assets.FocusKey);

        assets.Collapse();

        Assert.False(assets.Rows.Single(row => row.Key == "group:kind:Route").IsExpanded);

        assets.Expand();
        assets.Expand();

        Assert.Equal("north", assets.FocusKey);
    }

    [Fact]
    public async Task TheSearchUnfoldsTheHeadingsAndLeavesOutThoseWithNothingFound()
    {
        var assets = await LibraryAsync();

        assets.CollapseAll();

        Assert.All(assets.Rows, row => Assert.True(row is { IsHeading: true, IsExpanded: false }));

        assets.Filter = "nor";

        Assert.Equal("# Routes |  North", Shape(assets));
    }

    [Fact]
    public async Task LocatingAnAssetUnfoldsItsKindsHeading()
    {
        var assets = await LibraryAsync();

        assets.Toggle(assets.Rows.Single(row => row.Key == "group:kind:Zone"));
        assets.Locate("forest");

        Assert.Contains(assets.Rows, row => row is { Name: "Forest", IsSelected: true });
    }

    [Fact]
    public async Task MakingAFolderShowsTheFoldersAndRemembersIt()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");

        await SaveProjectAsync();

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer, AppPreferences.Load(path));

        main.Refresh();
        main.Assets.ClearSelection();
        main.Assets.NewFolderCommand.Execute(null);

        Assert.False(main.Assets.IsByKind);
        Assert.Contains(main.Assets.Rows, row => row.IsNewFolder);
        Assert.True(AppPreferences.Load(path).LibraryByFolder);
    }
}
