using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.Data.Tests;

namespace D3B.App.Tests.Hosting;

public sealed class AppPreferencesTests
{
    [Fact]
    public void TheAppRemembersItsLanguageAndLayoutFromOneRunToTheNext()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("D3B", "app.json");
        var first = AppPreferences.Load(path);

        Assert.Equal((null, null), (first.Language, first.Layout));

        first.RememberLanguage("fr");
        first.RememberLayout(new StudioLayoutState(260, 400, 0.5, 180, ["Inspector"]));

        var next = AppPreferences.Load(path);

        Assert.Equal("fr", next.Language);
        Assert.Equal((260d, 400d, 0.5, 180d), (next.Layout!.LeftWidth, next.Layout.RightWidth, next.Layout.LeftShare, next.Layout.BottomHeight));
        Assert.Equal(["Inspector"], next.Layout.Closed);
    }

    [Fact]
    public void AnOlderFilesThemeAndSidebarAreDroppedAtTheNextWrite()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");

        File.WriteAllText(path, """{ "Language": "fr", "Theme": "Light", "SidebarCollapsed": true, "Host": { "Delays:Display": "1.5" } }""");

        var preferences = AppPreferences.Load(path);

        preferences.RememberLanguage("en");

        var text = File.ReadAllText(path);

        Assert.DoesNotContain("Theme", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sidebar", text, StringComparison.Ordinal);
        Assert.Equal(("en", "1.5"), (AppPreferences.Load(path).Language, AppPreferences.Load(path).Host["Delays:Display"]));
    }

    [Fact]
    public void TheHostsSettingsAreRememberedAndForgottenOneByOne()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        var first = AppPreferences.Load(path);

        first.RememberLanguage("fr");
        first.RememberHost(new Dictionary<string, string?> { ["Delays:Display"] = "1.5", ["Delays:Answer"] = "6" });
        first.RememberHost(new Dictionary<string, string?> { ["Delays:Answer"] = null, ["Network:Captures:Enabled"] = "True" });

        var next = AppPreferences.Load(path);

        Assert.Equal("fr", next.Language);
        Assert.Equal(new Dictionary<string, string?> { ["Delays:Display"] = "1.5", ["Network:Captures:Enabled"] = "True" }, next.Host);
        Assert.Empty(first.Host.Keys.Except(next.Host.Keys));
    }

    [Fact]
    public void AFileThatDoesNotReadLeavesTheDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");

        File.WriteAllText(path, "{ not json");

        var preferences = AppPreferences.Load(path);

        Assert.Equal((null, null), (preferences.Language, preferences.Layout));
        Assert.Empty(preferences.Host);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "app.json"), AppPreferences.DefaultPath);
    }

    [Fact]
    public async Task TheWindowRemembersTheLanguageAndLayoutItIsLeftIn()
    {
        using var directory = new TemporaryDirectory();
        await using var store = new TestStore();
        var path = directory.Combine("app.json");
        var preferences = AppPreferences.Load(path);

        await store.OpenedAsync();

        var main = new MainViewModel(await TestHosts.RunningAsync(store.Host), new Localizer(Localizer.English), preferences);

        main.Settings.UseFrenchCommand.Execute(null);
        main.Layout.CloseCommand.Execute(StudioPanel.Details);

        var again = new MainViewModel(await TestHosts.RunningAsync(store.Host), new Localizer(Localizer.English), AppPreferences.Load(path));

        Assert.Equal("fr", AppPreferences.Load(path).Language);
        Assert.False(again.Layout.IsDetailsOpen);
        Assert.True(again.Layout.IsOutlinerOpen);

        again.Layout.ResetCommand.Execute(null);

        Assert.Null(AppPreferences.Load(path).Layout);
    }
}
