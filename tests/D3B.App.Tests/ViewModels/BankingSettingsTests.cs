using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Banks;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class BankingSettingsTests : IAsyncDisposable
{
    private static readonly string[] _texts = ["SettingBankFullAtRow", "SettingBankFullAtRowDetail", "SettingBankZaapsRow", "SettingBankZaapsRowDetail", "GroupBanking"];

    private readonly TestPlay _play = new();
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

    private async Task<(SettingsViewModel Settings, BankingPreferences Live)> RunningAsync(AppPreferences preferences, Action<BankingPreferences>? hostCommandLine = null)
    {
        var host = TestHosts.Of(_play);

        _hosts.Add(host);

        var live = host.Services.GetRequiredService<BankingPreferences>();

        hostCommandLine?.Invoke(live);

        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer, preferences);

        main.Refresh();

        return (main.Settings, live);
    }

    [Fact]
    public async Task TheBotsBankAtTheShareSetOnThePageAtOnceAndTheAppRemembersItForTheHost()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var (settings, live) = await RunningAsync(preferences);

        Assert.Equal(90m, settings.BankFullAt);
        Assert.False(settings.IsBankFullAtChanged);

        settings.BankFullAt = 98m;

        Assert.Equal(98, live.FullAt);
        Assert.True(settings.IsBankFullAtChanged);
        Assert.True(settings.Bots.HasChanges);
        Assert.Equal("98", AppPreferences.Load(PreferencesPath).Host[BankingPreferences.FullAtKey]);

        settings.ResetBankFullAtCommand.Execute(null);

        Assert.Equal(90, live.FullAt);
        Assert.False(settings.Bots.HasChanges);
        Assert.DoesNotContain(BankingPreferences.FullAtKey, AppPreferences.Load(PreferencesPath).Host.Keys);
    }

    [Fact]
    public async Task AShareOutsideItsBoundsOrEmptiedKeepsTheShareTheBotsHad()
    {
        var (settings, live) = await RunningAsync(AppPreferences.Load(PreferencesPath));

        settings.BankFullAt = 98m;
        settings.BankFullAt = 120m;
        settings.BankFullAt = null;

        Assert.Equal(98, live.FullAt);
    }

    [Fact]
    public async Task TheBotsTakeNoZaapToTheBankOnceTheBoxIsUnchecked()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var (settings, live) = await RunningAsync(preferences);

        Assert.True(settings.BankUseZaaps);

        settings.BankUseZaaps = false;

        Assert.False(live.UseZaaps);
        Assert.True(settings.IsBankZaapsOff);
        Assert.Equal("False", AppPreferences.Load(PreferencesPath).Host[BankingPreferences.UseZaapsKey]);

        settings.ResetBankZaapsCommand.Execute(null);

        Assert.True(live.UseZaaps);
    }

    [Fact]
    public async Task ThePageShowsTheSettingsTheHostRunsWith()
    {
        // As the host's command line would have set them.
        var (settings, live) = await RunningAsync(AppPreferences.Load(PreferencesPath), banking => (banking.FullAt, banking.UseZaaps) = (95, false));

        Assert.Equal(95m, settings.BankFullAt);
        Assert.False(settings.BankUseZaaps);
        Assert.False(live.UseZaaps);
    }

    [Fact]
    public async Task TheSearchFindsTheBankingRowsAndTheirTextsAreInBothLanguages()
    {
        var (settings, _) = await RunningAsync(AppPreferences.Load(PreferencesPath));

        settings.Filter = "zaap";

        Assert.True(settings.IsBankingShown);
        Assert.False(settings.BankFullAtRow.IsShown);
        Assert.True(settings.BankZaapsRow.IsShown);

        Assert.All(_texts, key =>
        {
            Assert.NotEqual($"[{key}]", new Localizer(Localizer.English)[key]);
            Assert.NotEqual($"[{key}]", new Localizer(Localizer.French)[key]);
            Assert.NotEqual(new Localizer(Localizer.English)[key], new Localizer(Localizer.French)[key]);
        });
    }
}
