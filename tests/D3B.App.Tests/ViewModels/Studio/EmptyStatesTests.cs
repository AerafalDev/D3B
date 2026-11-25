using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.ViewModels.World;
using D3B.Bot.Hosting;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
public sealed class EmptyStatesTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private MainViewModel Idle()
    {
        return new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);
    }

    [Fact]
    public void WithNoCharacterTheCharactersViewSaysHowToConnectOneThroughD3B()
    {
        var main = Idle();

        main.Refresh();

        var outliner = main.Outliner;

        Assert.Equal(OutlinerScope.Characters, outliner.Scope);
        Assert.True(outliner.IsGuiding);
        Assert.Equal("No character connected", outliner.EmptyTitle);
        Assert.Equal(["1. Keep D3B open: its status bar says « Relay ready ».", "2. Start Dofus from Ankama's launcher.", "3. Choose a character: it shows here."], outliner.EmptySteps);
        Assert.False(outliner.ShowsMakeButtons);

        // A search that finds nothing says so, and guides nowhere.
        outliner.Filter = "zz";

        Assert.Equal((false, "Nothing matches “zz”"), (outliner.IsGuiding, outliner.EmptyText));
    }

    [Fact]
    public async Task WithNoRouteTheWorldViewOffersToMakeARouteOrAZone()
    {
        await _store.OpenedAsync();

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer);

        main.Routes.Use(TestRouteWorld.Services(_store.Assets));
        main.Refresh();
        main.Layout.ShowView(SideView.World);
        main.Refresh();

        Assert.Equal(("No route or zone yet", true, true), (main.Outliner.EmptyTitle, main.Outliner.IsGuiding, main.Outliner.ShowsMakeButtons));

        await main.Outliner.MakeRouteCommand.ExecuteAsync(null);

        Assert.True(main.Editor.IsShown("world"));
    }

    [Fact]
    public async Task WithNoProfileTheLibraryOffersAButtonThatMakesOne()
    {
        await _store.OpenedAsync();

        var main = new MainViewModel(await TestHosts.RunningAsync(_store.Host), _localizer);

        main.Refresh();

        Assert.True(main.Assets.IsProjectEmpty);

        main.Assets.NewProfileCommand.Execute(null);

        Assert.IsType<NewProfileViewModel>(main.Dialogs.Current);
    }

    [Fact]
    public void WithNothingInItsPeriodTheStatisticsOfferEveryRunAndTheJournalSaysWhatFillsIt()
    {
        var main = Idle();

        main.ShowStatistics();

        Assert.True(main.Statistics.ShowsAllRuns);

        main.Statistics.ShowAllCommand.Execute(null);

        Assert.False(main.Statistics.ShowsAllRuns);
        Assert.Equal("Each character's actions show here as soon as it plays.", _localizer["LogsEmpty"]);
    }
}
