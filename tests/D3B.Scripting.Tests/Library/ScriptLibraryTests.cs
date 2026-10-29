using D3B.Bot.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Scripting.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Scripting.Tests.Library;

public sealed class ScriptLibraryTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly ScriptLibrary _library;
    private readonly IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    public ScriptLibraryTests()
    {
        _library = new ScriptLibrary(new ScriptingSettings { Directory = _directory.Path }, NullLogger<ScriptLibrary>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _library.StopAsync(TestContext.Current.CancellationToken);
        _library.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public async Task TheFolderListsItsScriptsAsActivitiesAndHoldsTheApisDefinitions()
    {
        Write("stroll.lua", "settings = { { key = \"walks\", kind = \"number\", min = 1, max = 9, default = 2 } }\nfunction step(bot) end");
        Write(Path.Combine("jobs", "miner.lua"), "function step(bot) end");
        Write(Path.Combine("jobs", "broken.lua"), "function step(\n");
        Write("nostep.lua", "local x = 1");

        await _library.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["script:jobs/broken.lua", "script:jobs/miner.lua", "script:nostep.lua", "script:stroll.lua"], _library.Activities.Select(activity => activity.Id));
        Assert.Equal(["jobs/broken", "jobs/miner", "nostep", "stroll"], _library.Activities.Select(activity => activity.Name.ToString()));
        Assert.Equal(["walks"], _library.Activities[3].Settings.All.Select(setting => setting.Key));
        Assert.Equal(["script:jobs/broken.lua", "script:nostep.lua"], _library.Problems.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ScriptDefinitions.Lua, File.ReadAllText(_directory.Combine("d3b.lua")));
        Assert.Equal(ScriptDefinitions.Settings, File.ReadAllText(_directory.Combine(".luarc.json")));

        // A script that does not read is listed, and its start says why.
        var error = Assert.Throws<ScriptException>(() => _library.Activities[2].Create(_services));

        Assert.Equal("nostep: the script defines no step(bot) function, which the bot calls for each step.", error.Message);
    }

    [Fact]
    public async Task AFileSavedInTheFolderIsReadAgain()
    {
        Write("stroll.lua", "function step(bot) end");
        await _library.StartAsync(TestContext.Current.CancellationToken);

        var version = _library.Version;

        Write("stroll.lua", "function step(\n");
        Write("new.lua", "function step(bot) end");
        await WaitForAsync(() => _library.Version > version && _library.Activities.Count == 2 && _library.Problems.Count == 1);

        File.Delete(_directory.Combine("new.lua"));
        await WaitForAsync(() => _library.Activities.Count == 1);

        Assert.Equal(["script:stroll.lua"], _library.Activities.Select(activity => activity.Id));
    }

    [Fact]
    public async Task ATextThatRunsOnIsStoppedAndSaysSo()
    {
        Write("loop.lua", "while true do end");
        await _library.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("loop: its text ran more than 2 s; a script's text defines its hooks and settings, then ends.", _library.Problems["script:loop.lua"].Message);
    }

    [Fact]
    public async Task AProfilesSettingsReachTheScript()
    {
        Write("stroll.lua", """
            settings = { { key = "walks", kind = "number", min = 1, max = 9, default = 2 } }

            function step(bot)
              log.info("walks " .. settings.walks)
              return false
            end
            """);
        await _library.StartAsync(TestContext.Current.CancellationToken);

        var definition = _library.Activities[0];
        var reading = definition.Settings.Read("""{"walks":7}""");

        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(300);
        var activity = Assert.IsType<ScriptActivity>(definition.Create(_services, reading.Settings));

        Assert.Equal(ActivityOutcome.Done, await bot.StartActivity(activity).Completion);
        Assert.Equal(["walks 7"], activity.Console.Lines().Select(line => line.Text));
    }

    [Fact]
    public async Task TheCatalogListsTheScriptsAfterTheModulesActivities()
    {
        Write("stroll.lua", "function step(bot) end");
        await _library.StartAsync(TestContext.Current.CancellationToken);

        var catalog = new ActivityCatalog([new ActivityDefinition("patrol", "Patrol", _ => throw new InvalidOperationException())], [_library]);

        Assert.Equal(["patrol", "script:stroll.lua"], catalog.All.Select(activity => activity.Id));
        Assert.True(catalog.TryGet("script:stroll.lua", out _));
        Assert.Equal(_library.Version, catalog.Version);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The library never read the folder again.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private void Write(string path, string text)
    {
        var full = _directory.Combine(path);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
