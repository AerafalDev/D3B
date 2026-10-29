using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Scripting;
using D3B.Scripting.Library;

namespace D3B.App.Tests.ViewModels;

public sealed class ScriptProfilesTests : IAsyncDisposable
{
    private const string Source = """
        settings = {
          { section = { fr = "Promenade", en = "Stroll" } },
          { key = "walks", kind = "number", name = { fr = "Marches", en = "Walks" }, min = 1, max = 100, default = 10 },
          { key = "city", kind = "choice", name = { fr = "Ville", en = "City" }, options = { "astrub", { value = "bonta", name = "Bonta" } } },
        }

        function step(bot) return false end
        """;

    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.French);

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _game.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public async Task AScriptsSettingsAreEditedInAProfileAndStoredByKey()
    {
        var (definition, error) = await ScriptReader.ReadAsync("script:promenade.lua", new LuaScript("promenade", Source), TestContext.Current.CancellationToken);

        Assert.Null(error);

        using var host = await TestHosts.WithStorageAsync(_play, _directory.Combine("d3b.db"), _game, definition);
        var page = new ProfilesViewModel(_localizer);

        page.Refresh(host.Services);
        page.NewCommand.Execute(null);

        var item = Assert.Single(page.Items);

        Assert.Equal("promenade", item.EditActivity?.Name);
        Assert.Equal(["Promenade"], item.Editor!.Sections.Select(section => section.Title));

        var fields = item.Editor.Sections.SelectMany(section => section.Fields).ToList();
        var walks = Assert.IsType<NumberFieldViewModel>(fields[0]);
        var city = Assert.IsType<ChoiceFieldViewModel>(fields[1]);

        Assert.Equal(("Marches", 10m), (walks.Label, walks.Value));
        Assert.Equal(["astrub", "Bonta"], city.Options.Select(option => option.Name));
        Assert.Equal("astrub", city.Selected?.Value);

        walks.Value = 3;
        city.Selected = city.Options[1];
        item.EditName = "Courte promenade";

        Assert.True(item.CanSave);

        await item.SaveCommand.ExecuteAsync(null);

        Assert.Equal("""{"walks":3,"city":"bonta"}""", Assert.Single(page.Profiles!.Profiles).Settings);
    }
}
