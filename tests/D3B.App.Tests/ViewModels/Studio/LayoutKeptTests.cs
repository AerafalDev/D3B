using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.Studio.Tests.Behaviors;
using D3B.Data.Tests;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
public sealed class LayoutKeptTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        _directory.Dispose();
    }

    private async Task<MainViewModel> StartAsync()
    {
        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer, AppPreferences.Load(_directory.Combine("app.json")));

        // The pages are read on the first refresh the host runs, the tabs come back on the next.
        main.Refresh();
        main.Refresh();

        return main;
    }

    [Fact]
    public async Task TheTabsOpenTheOneShownAndTheBottomPanelsTabComeBackAfterARestart()
    {
        await _store.OpenedAsync();

        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", TestContext.Current.CancellationToken);
        var first = await StartAsync();

        first.ShowHome();
        first.OpenProfile(walker.Id);
        first.ShowStatistics();
        first.ShowSettings();
        first.Editor.Select(first.Editor.Items.Single(tab => tab.Key == "profile:" + walker.Id));
        first.Bottom.Show(BottomTab.Problems);
        first.Bottom.Close();

        var next = await StartAsync();

        Assert.Equal(["home", "profile:" + walker.Id, "statistics", "settings"], next.Editor.Items.Select(tab => tab.Key));
        Assert.Equal("profile:" + walker.Id, next.Editor.Selected?.Key);
        Assert.Equal((BottomTab.Problems, false), (next.Bottom.Tab, next.Layout.IsBottomOpen));
    }

    [Fact]
    public async Task ATabWhoseSubjectIsGoneFallsBackQuietly()
    {
        await _store.OpenedAsync();

        AppPreferences.Load(_directory.Combine("app.json")).RememberTabs(new StudioTabsState(["profile:gone", "character:1-2", "page:gone", "settings"], "profile:gone", "Sequencer"));

        var main = await StartAsync();

        Assert.Equal(["settings"], main.Editor.Items.Select(tab => tab.Key));
        Assert.Equal("settings", main.Editor.Selected?.Key);
        Assert.Equal(BottomTab.Journal, main.Bottom.Tab);
        Assert.False(main.Dialogs.IsOpen);
    }

    [Fact]
    public async Task TheTabsOfTheLastRunAreNotForgottenBeforeTheyComeBack()
    {
        await _store.OpenedAsync();

        var path = _directory.Combine("app.json");

        AppPreferences.Load(path).RememberTabs(new StudioTabsState(["settings", "statistics"], "statistics"));

        // While the host gets ready, a tab opened by hand does not write over the last run's.
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<D3B.Bot.Hosting.BotHostStart>().Task), _localizer, AppPreferences.Load(path));

        main.ShowHome();

        Assert.Equal(["settings", "statistics"], AppPreferences.Load(path).Tabs!.Open);
    }
}
