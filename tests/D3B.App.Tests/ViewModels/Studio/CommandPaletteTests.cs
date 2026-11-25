using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
public sealed class CommandPaletteTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static IReadOnlyList<string> Titles(MainViewModel main)
    {
        return [.. main.Search.Groups.SelectMany(group => group.Results).Select(result => result.Item.Title)];
    }

    private static void Run(MainViewModel main, string title)
    {
        main.Search.Open(SearchScope.Commands);
        main.Search.Query = title;
        main.Search.Run(main.Search.Groups.SelectMany(group => group.Results).First(result => result.Item.Title == title));
    }

    [Fact]
    public void CtrlShiftPOpensThePaletteOnTheCommandsAndDeletingItsSignSearchesEverything()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.ShowCommandPaletteCommand.Execute(null);

        // As VS Code's: the palette is the search with its sign typed, which a backspace takes away.
        Assert.Equal((true, ">", SearchScope.Commands), (main.Search.IsOpen, main.Search.Query, main.Search.ShownScope));
        Assert.Equal("Show all commands", Titles(main)[0]);

        main.Search.Query = "";

        Assert.Equal(SearchScope.All, main.Search.ShownScope);
    }

    [Fact]
    public void TheStudiosViewsAreCommandsOfThePalette()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.Layout.Close(StudioPanel.Side);
        Run(main, "Show the library");

        Assert.Equal((true, SideView.Library), (main.Layout.IsSideOpen, main.Layout.View));

        Run(main, "Show the world");

        Assert.Equal(SideView.World, main.Layout.View);

        Run(main, "Show the home page");

        Assert.True(main.Editor.IsShown("home"));

        Run(main, "Show the problems");

        Assert.Equal((true, BottomTab.Problems), (main.Layout.IsBottomOpen, main.Bottom.Tab));

        Run(main, "Show the trace");

        Assert.Equal(BottomTab.Trace, main.Bottom.Tab);

        // Without the advanced tools, the protocol is offered nowhere (D513).
        main.ShowCommandPalette();

        Assert.DoesNotContain("Show the protocol", Titles(main));

        main.Layout.ToggleAdvancedTools();
        Run(main, "Show the protocol");

        Assert.Equal(BottomTab.Protocol, main.Bottom.Tab);
    }

    [Fact]
    public void TheSearchSpeaksOfTheLibraryNotOfAssets()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.Search.Open();

        Assert.Equal(["All", "Library", "Characters", "Maps", "Commands", "Settings"], main.Search.Chips.Select(chip => chip.Name));
    }

    [Fact]
    public async Task ACharacterIsPlayedPausedAndStoppedFromThePalette()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var (_, bot) = await _play.EnterAsync(300);
        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer);

        main.Refresh();
        main.ShowCommandPalette();

        Assert.Contains("Play Character", Titles(main));
        Assert.DoesNotContain("Stop Character", Titles(main));

        // A character's command shows its portrait, as its own result does.
        var play = main.Search.Groups.SelectMany(group => group.Results).Single(result => result.Item.Title == "Play Character").Item;

        Assert.NotEqual("", play.Initials);

        Run(main, "Play Character");
        await activity.SteppingAsync(bot);
        main.Refresh();

        Assert.Equal(bot.Key, main.RunBlock.ChosenKey);

        Run(main, "Pause Character");
        await Waiting.UntilAsync(() => bot.IsPaused);
        main.Refresh();
        Run(main, "Stop Character");

        Assert.Equal(ActivityOutcome.Stopped, await bot.Activity!.Completion);
    }

    [Fact]
    public async Task ACharacterOfflineIsForgottenFromThePaletteAfterAQuestion()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();
        await store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));

        var main = new MainViewModel(await TestHosts.RunningAsync(store.Host), _localizer);

        main.Refresh();
        Run(main, "Forget Aerafa's account");

        Assert.True(main.Dialogs.IsOpen);
    }
}
