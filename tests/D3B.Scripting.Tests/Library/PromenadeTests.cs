using D3B.Bot.Activities;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Scripting.Library;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Scripting.Tests.Library;

public sealed class PromenadeTests
{
    [Fact]
    public async Task TheStrollWalksAsManyTimesAsItsSettingsSayWithinItsReachAwayFromMapActions()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "samples", "promenade.lua"), TestContext.Current.CancellationToken);
        var (definition, error) = await ScriptReader.ReadAsync("script:promenade.lua", new LuaScript("promenade", source), TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal(["walks", "reach"], definition.Settings.All.Select(setting => setting.Key));

        var settings = definition.Settings.Read("""{"walks":3,"reach":2}""").Settings;

        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(409 - 28);
        using var activity = Assert.IsType<ScriptActivity>(definition.Create(new ServiceCollection().BuildServiceProvider(), settings));
        var run = bot.StartActivity(activity);
        var from = 409 - 28;

        for (var walk = 1; walk <= 3; walk++)
        {
            var to = await TestWalks.WalkedAsync(play, connection, walk);

            Assert.InRange(MapGeometry.Distance(from, to), 1, 2);
            Assert.NotEqual(TestPlay.ActionCell, to);
            from = to;
        }

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(3, activity.Console.Lines().Count(line => line.Text.EndsWith(": arrived", StringComparison.Ordinal)));
    }
}
