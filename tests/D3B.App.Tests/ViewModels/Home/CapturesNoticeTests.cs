using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Home;
using D3B.Data.Tests;

namespace D3B.App.Tests.ViewModels.Home;

public sealed class CapturesNoticeTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);

    private string PreferencesPath => _directory.Combine("app.json");

    public void Dispose()
    {
        _directory.Dispose();
    }

    private (CapturesNoticeViewModel Notice, SettingsViewModel Settings) Notice(AppPreferences preferences)
    {
        var settings = new SettingsViewModel(_localizer, preferences, _ => { }, new Toasts());
        var notice = new CapturesNoticeViewModel(_localizer, settings, preferences);

        notice.Refresh();

        return (notice, settings);
    }

    [Fact]
    public void TheFirstStartSaysTheSessionsAreRecordedHereAndWhatLeavesNothingUnlessAReportIsSent()
    {
        var (notice, _) = Notice(AppPreferences.Load(PreferencesPath));

        Assert.True(notice.IsShown);
        Assert.Contains("recorded on this machine", notice.Text, StringComparison.Ordinal);
        Assert.Contains("Kept 7 days, up to 1,024 MB", notice.Text, StringComparison.Ordinal);
        Assert.Contains("Nothing leaves this machine", notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnderstoodHidesItForGood()
    {
        var (notice, _) = Notice(AppPreferences.Load(PreferencesPath));

        notice.UnderstandCommand.Execute(null);

        Assert.False(notice.IsShown);
        Assert.True(AppPreferences.Load(PreferencesPath).CapturesNoticeSeen);

        var (next, _) = Notice(AppPreferences.Load(PreferencesPath));

        Assert.False(next.IsShown);
    }

    [Fact]
    public void ThereIsNothingToSayWhenTheSessionsAreNotRecorded()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var (notice, settings) = Notice(preferences);

        settings.RecordsCaptures = false;
        notice.Refresh();

        Assert.False(notice.IsShown);
    }

    [Fact]
    public void TheSettingsButtonAsksForTheNetworkSettings()
    {
        var (notice, _) = Notice(AppPreferences.Load(PreferencesPath));
        var asked = 0;

        notice.SettingsRequested = () => asked++;
        notice.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, asked);
    }

    [Fact]
    public void TheTextFollowsTheLanguageAndTheRetention()
    {
        var (notice, settings) = Notice(AppPreferences.Load(PreferencesPath));

        settings.CaptureDays = 2m;
        _localizer.Use(Localizer.French);
        notice.Refresh();

        Assert.Contains("2 jours", notice.Text, StringComparison.Ordinal);

        _localizer.Use(Localizer.English);
    }
}
