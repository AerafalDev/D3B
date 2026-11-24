using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Data;
using D3B.Data.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class ClientFolderSettingsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly Toasts _toasts = new();

    private string PreferencesPath => _directory.Combine("app.json");

    public void Dispose()
    {
        _directory.Dispose();
    }

    private SettingsViewModel Settings()
    {
        return new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, _toasts);
    }

    private string InstalledClient(string name)
    {
        var client = _directory.Combine(name);
        var streamingAssets = Path.Combine(client, "Dofus_Data", "StreamingAssets");

        Directory.CreateDirectory(streamingAssets);
        File.WriteAllText(Path.Combine(streamingAssets, "version"), "Version=3.7.14.18\n");

        return client;
    }

    [Fact]
    public void WithNoFolderChosenThePageShowsTheOneTheLauncherGives()
    {
        var settings = Settings();

        Assert.False(settings.IsClientFolderChosen);
        Assert.Equal(ClientLocator.Locate(null), settings.ClientFolderPath);
        Assert.False(settings.Files.HasChanges);
    }

    [Fact]
    public void AFolderHoldingTheClientIsRememberedForTheHostsNextStart()
    {
        var settings = Settings();
        var client = InstalledClient("Dofus-dofus3");

        Assert.True(settings.ChooseClientFolder(client));

        Assert.True(settings.IsClientFolderChosen);
        Assert.Equal(client, settings.ClientFolderPath);
        Assert.True(settings.Files.HasChanges);
        Assert.Equal(client, AppPreferences.Load(PreferencesPath).Host[ClientLocator.ConfigurationKey]);
        Assert.Equal(client, Settings().ClientFolderPath);
    }

    [Fact]
    public void AFolderWithoutTheClientIsRefusedAndSaidSo()
    {
        var settings = Settings();
        var empty = _directory.Combine("Empty");

        Directory.CreateDirectory(empty);

        Assert.False(settings.ChooseClientFolder(empty));

        Assert.False(settings.IsClientFolderChosen);
        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);
        Assert.Contains(empty, _toasts.Recent[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderTheHostsCommandLineNamesIsTheOneShown()
    {
        var settings = Settings();
        var client = InstalledClient("FromCommandLine");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ClientLocator.ConfigurationKey] = client }).Build();

        settings.Refresh(new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider());

        Assert.Equal(client, settings.ClientFolderPath);
    }

    [Fact]
    public void AFolderWithADotInItsNameOpensAsAFolder()
    {
        var settings = Settings();
        var client = InstalledClient("Dofus 3.0");
        string? opened = null;

        settings.OpenFolder = folder => opened = folder;
        settings.ChooseClientFolder(client);
        settings.OpenFolderPathCommand.Execute(settings.ClientFolderPath);

        Assert.Equal(client, opened);
    }

    [Fact]
    public void ResettingTheFolderGivesTheLaunchersBack()
    {
        var settings = Settings();

        var client = InstalledClient("Dofus-dofus3");

        settings.ChooseClientFolder(client);
        settings.ResetClientFolderCommand.Execute(null);

        Assert.False(settings.IsClientFolderChosen);
        Assert.NotEqual(client, settings.ClientFolderPath);
        Assert.Equal(ClientLocator.Locate(null), settings.ClientFolderPath);
        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);
    }
}
