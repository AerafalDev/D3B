using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels.Characters;

[UiThread]
public sealed class CharacterPlayTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheCharactersTabPlaysPausesAndStopsItWithTheButtonsItsStateAllows()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var (_, bot) = await _play.EnterAsync(300);
        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer);

        main.Refresh();
        main.OpenCharacter(bot.Key);
        main.Refresh();

        var tab = Assert.IsType<CharacterTabViewModel>(main.Editor.Selected?.Content);

        Assert.Equal((true, true, false, false), (tab.ShowsPlay, tab.CanPlay, tab.ShowsPause, tab.ShowsStop));
        Assert.Equal("Play Patrol", tab.PlayTip);

        tab.PlayCommand.Execute(null);
        await activity.SteppingAsync(bot);
        main.Refresh();

        Assert.Equal((false, true, true), (tab.ShowsPlay, tab.ShowsPause, tab.ShowsStop));
        // Played from its tab, the character is the one the run block drives, so that F5 and F6 act on it too;
        // the Details, which would say again what the header says, stay as they were.
        Assert.Equal(bot.Key, main.RunBlock.ChosenKey);
        Assert.False(main.Layout.IsDetailsOpen);

        tab.PauseCommand.Execute(null);

        // The pause goes through the bot's loop.
        await Waiting.UntilAsync(() => bot.IsPaused);
        main.Refresh();

        Assert.Equal((true, true, false, true), (tab.ShowsPlay, tab.CanPlay, tab.ShowsPause, tab.ShowsStop));
        Assert.Equal("Resume", tab.PlayTip);

        tab.StopCommand.Execute(null);
        Assert.Equal(ActivityOutcome.Stopped, await bot.Activity!.Completion);
        main.Refresh();

        Assert.Equal((true, false), (tab.ShowsPlay, tab.ShowsStop));
    }

    [Fact]
    public async Task TheCharactersTabNamesItsProfileAndOffersTheOthers()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var (_, bot) = await _play.EnterAsync(300);
        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer);

        main.Refresh();
        main.OpenCharacter(bot.Key);
        main.Refresh();

        var tab = Assert.IsType<CharacterTabViewModel>(main.Editor.Selected?.Content);

        var character = Assert.IsType<D3B.App.ViewModels.Properties.CharacterPropertiesViewModel>(tab.Character);

        Assert.Equal(bot.Key, character.Key);
        Assert.Equal("Patrol", character.Profile?.Name);
        Assert.Equal("Patrol", Assert.Single(character.ActivityProfiles).Name);
    }

    [Fact]
    public async Task TheHomePagesCardOfACharacterOnlineNamesItsProfileAndPlaysAndPausesIt()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var (_, bot) = await _play.EnterAsync(300);
        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer);

        main.Refresh();

        var card = Assert.Single(main.Home.Characters);

        Assert.Equal((true, true, false, "Patrol"), (card.IsOnline, card.CanPlay, card.IsPlaying, card.Profile));

        card.PlayOrPauseCommand.Execute(null);
        await activity.SteppingAsync(bot);
        main.Refresh();
        card = Assert.Single(main.Home.Characters);

        Assert.True(card.IsPlaying);
        Assert.Equal(bot.Key, main.RunBlock.ChosenKey);
        Assert.False(main.Layout.IsDetailsOpen);

        card.PlayOrPauseCommand.Execute(null);
        await Waiting.UntilAsync(() => bot.IsPaused);
        main.Refresh();

        Assert.False(Assert.Single(main.Home.Characters).IsPlaying);
    }
}
