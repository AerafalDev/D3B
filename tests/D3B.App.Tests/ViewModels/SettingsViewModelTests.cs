using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.AppSettings;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Network.Relay;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class SettingsViewModelTests : IAsyncDisposable
{
    // The page is tested on the bots' delays as measured, the wait between two steps included.
    private readonly TestPlay _play = new() { Bots = { Delays = BotDelays.Measured } };
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly List<IHost> _hosts = [];

    private string PreferencesPath => _directory.Combine("app.json");

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
            host.Dispose();

        await _play.DisposeAsync();
        _directory.Dispose();
    }

    private IHost TestHost()
    {
        var host = TestHosts.Of(_play);

        _hosts.Add(host);

        return host;
    }

    private async Task<MainViewModel> RunningAsync(AppPreferences preferences)
    {
        var main = new MainViewModel(await TestHosts.RunningAsync(TestHost()), _localizer, preferences);

        main.Refresh();

        return main;
    }

    [Fact]
    public async Task TheBotsWaitTheDelaysChangedOnThePageAtOnceAndTheAppRemembersThemForTheHost()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var settings = (await RunningAsync(preferences)).Settings;
        var answer = settings.BotWaits[0];

        Assert.Equal(0.5m, settings.Display.Value);
        Assert.Equal("Measured: 0.5 s", settings.Display.DefaultText);
        Assert.Equal("Up to 3 s", settings.Display.MostText);
        Assert.False(settings.HasRaisedDelays);

        settings.Display.Value = 1.5m;
        answer.Value = 6m;

        Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(1.5), Answer = TimeSpan.FromSeconds(6) }, _play.Bots.Delays);
        Assert.True(answer.IsRaised);
        Assert.True(settings.HasRaisedDelays);
        Assert.Equal("1.5 s", settings.Display.ValueText);
        Assert.Equal(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "1.5", [BotDelays.AnswerKey] = "6" }, AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task ADelayIsNeverLoweredBelowItsDefaultNorRaisedPastItsMost()
    {
        var settings = (await RunningAsync(AppPreferences.Load(PreferencesPath))).Settings;

        settings.Display.Value = 0.1m;

        Assert.Equal(BotDelays.Measured, _play.Bots.Delays);

        settings.Display.Value = 9m;

        Assert.Equal(BotDelays.Most.Display, _play.Bots.Delays.Display);
        Assert.Equal((0.5m, 3m, 0.1m), (settings.Display.Minimum, settings.Display.Maximum, settings.Display.Increment));
    }

    [Fact]
    public async Task RestoringTheDelaysGivesBackTheDefaultsAndForgetsThemButKeepsTheDisplayTime()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var settings = (await RunningAsync(preferences)).Settings;
        var hold = settings.RelayWaits[2];

        settings.Display.Value = 2m;
        settings.BotWaits[0].Value = 6m;
        hold.Value = 10m;

        Assert.Equal("Default: 5 s", hold.DefaultText);
        Assert.True(settings.RestoreDelaysCommand.CanExecute(null));

        settings.RestoreDelaysCommand.Execute(null);

        Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(2) }, _play.Bots.Delays);
        Assert.Equal((3m, 5m), (settings.BotWaits[0].Value, hold.Value));
        Assert.False(settings.RestoreDelaysCommand.CanExecute(null));
        Assert.Equal(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "2" }, AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task ThePageShowsTheDelaysTheBotsWaitOnceTheHostRuns()
    {
        var preferences = AppPreferences.Load(PreferencesPath);

        preferences.RememberHost(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "1", [RelayTimeouts.ConnectKey] = "20" });

        // As the host's command line would have raised it.
        _play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(2.5) };

        var settings = (await RunningAsync(preferences)).Settings;

        Assert.Equal(2.5m, settings.Display.Value);
        Assert.Equal(20m, settings.RelayWaits[0].Value);
        Assert.Equal(TimeSpan.FromSeconds(2.5), _play.Bots.Delays.Display);
    }

    [Fact]
    public void ADelayChangedBeforeTheHostRanIsGivenToItsBots()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer, AppPreferences.Load(PreferencesPath));
        var settings = main.Settings;

        main.Refresh();
        settings.Display.Value = 1m;
        Assert.Equal("Known once the host runs", settings.PackPath);

        settings.Refresh(TestHost().Services);

        Assert.Equal(TimeSpan.FromSeconds(1), _play.Bots.Delays.Display);
        Assert.Equal(1m, settings.Display.Value);
    }

    [Fact]
    public void AFileEditedToADelayOutOfItsBoundsLeavesThePageOnTheDefaults()
    {
        var preferences = AppPreferences.Load(PreferencesPath);

        preferences.RememberHost(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "0.1", [RelayTimeouts.HoldKey] = "600" });

        var settings = new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, new Toasts());

        Assert.Equal(0.5m, settings.Display.Value);
        Assert.Equal(5m, settings.RelayWaits[2].Value);
    }

    [Fact]
    public async Task CapturesAreRememberedForTheNextStart()
    {
        var settings = (await RunningAsync(AppPreferences.Load(PreferencesPath))).Settings;

        Assert.False(settings.RecordsCaptures);
        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);

        settings.RecordsCaptures = true;

        Assert.Equal("True", AppPreferences.Load(PreferencesPath).Host[RelaySettings.CapturesEnabledKey]);
        Assert.True(new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, new Toasts()).RecordsCaptures);

        settings.RecordsCaptures = false;

        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task TheLanguageChangesFromTheSettingsAndTheStudiosTabsFollowIt()
    {
        var main = await RunningAsync(AppPreferences.Load(PreferencesPath));
        var settings = main.Settings;

        Assert.True(settings.IsEnglish);

        settings.UseFrenchCommand.Execute(null);

        Assert.True(settings.IsFrench);
        Assert.Equal("Temps d'affichage", settings.Display.Label);
        Assert.Equal("Mesuré : 0,5 s", settings.Display.DefaultText);
        Assert.Equal("Accueil", main.Editor.Selected?.Title);
        Assert.Equal("fr", AppPreferences.Load(PreferencesPath).Language);

        settings.UseEnglishCommand.Execute(null);

        Assert.True(settings.IsEnglish);
        Assert.Equal("Display time", settings.Display.Label);
        Assert.Equal("Home", main.Editor.Selected?.Title);
    }

    [Fact]
    public void TheSearchKeepsTheRowsAndSectionsThatHoldItsTextAndASectionShownClearsIt()
    {
        var settings = new SettingsViewModel(_localizer, null, _ => { }, new Toasts());

        Assert.Equal(SettingsSection.Bots, settings.Selected.Section);

        settings.Filter = "database";

        Assert.Equal([SettingsSection.Files], settings.Sections.Where(section => section.IsShown).Select(section => section.Section));
        Assert.Equal(SettingsSection.Files, settings.Selected.Section);
        Assert.True(settings.Database.IsShown);
        Assert.False(settings.Project.IsShown);
        Assert.False(settings.Display.IsShown);

        settings.Filter = "server's answer";

        Assert.Equal(SettingsSection.Bots, settings.Selected.Section);
        Assert.Equal(["Server's answer"], settings.BotWaits.Where(delay => delay.IsShown).Select(delay => delay.Label));

        settings.Filter = "nothing at all";

        Assert.True(settings.HasNoMatch);

        settings.ShowCommand.Execute(SettingsSection.About);

        Assert.Equal(("", SettingsSection.About, false), (settings.Filter, settings.Selected.Section, settings.HasNoMatch));
        Assert.Equal([false, false, false, false, true], settings.Sections.Select(section => section.IsSelected));

        settings.ChooseCommand.Execute(SettingsSection.Network);

        Assert.True(settings.Network.IsSelected);
    }

    [Fact]
    public void AValueChangedMarksItsSectionAndItsArrowGivesItsDefaultBack()
    {
        var settings = new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, new Toasts());
        var answer = settings.BotWaits[0];

        Assert.False(settings.Bots.HasChanges);
        Assert.False(answer.ResetCommand.CanExecute(null));

        answer.IncreaseCommand.Execute(null);

        Assert.Equal((3.5m, true, true), (answer.Value, answer.IsRaised, settings.Bots.HasChanges));
        Assert.Equal("Reset to 3 s", answer.ResetText);
        Assert.Equal("Wait for the server to answer a request. Measured: 3 s", answer.Detail);

        answer.DecreaseCommand.Execute(null);

        Assert.False(settings.Bots.HasChanges);

        settings.Display.Value = 2m;

        Assert.True(settings.Bots.HasChanges);

        settings.Display.ResetCommand.Execute(null);

        Assert.Equal((0.5m, false), (settings.Display.Value, settings.Bots.HasChanges));

        settings.RecordsCaptures = true;

        Assert.True(settings.Network.HasChanges);

        settings.ResetCapturesCommand.Execute(null);

        Assert.Equal((false, false), (settings.RecordsCaptures, settings.Network.HasChanges));
        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public void AFileOpensItsFolderAndTheLayoutAndLicencesAreTheWindowsToOpen()
    {
        var settings = new SettingsViewModel(_localizer, null, _ => { }, new Toasts());
        var opened = new List<string>();
        var reset = 0;

        settings.OpenFolder = opened.Add;
        settings.OpenFile = opened.Add;
        settings.ResetLayout = () => reset++;
        settings.OpenPathCommand.Execute(@"C:\Users\Player\AppData\Local\D3B\d3b.db");
        settings.OpenPathCommand.Execute(@"C:\Users\Player\AppData\Local\D3B\studio");
        settings.OpenLicencesCommand.Execute(null);
        settings.ResetLayoutToDefaultCommand.Execute(null);

        Assert.Equal([@"C:\Users\Player\AppData\Local\D3B", @"C:\Users\Player\AppData\Local\D3B\studio", settings.LicencesPath], opened);
        Assert.EndsWith("THIRD-PARTY-NOTICES.md", settings.LicencesPath, StringComparison.Ordinal);
        Assert.Equal(1, reset);
        Assert.Equal(("Known once the host runs", false), (settings.ClientDataPath, settings.IsPackKnown));
    }

    [Fact]
    public async Task TheSettingsTabShowsWhetherTheHostRunsOrNot()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.ShowSettingsCommand.Execute(null);

        Assert.Equal(("settings", false), (main.Editor.Selected?.Key, main.Editor.Selected?.NeedsHost));

        main.ShowHomeCommand.Execute(null);

        Assert.Equal("home", main.Editor.Selected?.Key);

        var running = await RunningAsync(AppPreferences.Load(PreferencesPath));

        running.ShowSettingsCommand.Execute(null);
        running.Bottom.ShowCommand.Execute(BottomTab.Journal);

        Assert.Equal(("settings", true, BottomTab.Journal), (running.Editor.Selected?.Key, running.Layout.IsBottomOpen, running.Bottom.Tab));
    }
}
