using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Scripting.Tests;

public sealed class ScriptActivityTests : IAsyncDisposable
{
    /// <summary>
    /// A piece of the owner's miner script, written again in D3B's API: the mine of Herale's custom step, which leaves its
    /// map by one way, then by the other each time the character comes back, and the steps around it, the maps those of
    /// the test's world, known by coordinates or by id.
    /// </summary>
    private const string Herale = """
        log.info("Bonjour !")

        local east = true

        function step(bot)
          local map = bot.map

          if map.x == 4 and map.y == -18 then
            if east then
              east = false
              bot:walk(409)
            else
              east = true
              bot:changeMap("top")
            end
          elseif map.id == 191104000 then
            bot:changeMap("bottom")
          elseif map.x == 5 and map.y == -18 then
            bot:changeMap("left")
          end
        end
        """;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheScriptsStepsAwaitTheBotsBehaviors()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        using var activity = Script(Herale);
        var run = bot.StartActivity(activity);

        // On the first map, the step walks onto the map action east, and waits for the walk.
        var walk = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 1));

        Assert.Equal(TestPlay.ActionCell, walk.KeyMovements[^1] & 0xFFF);
        Assert.Equal(0, run.Steps);

        await TestWalks.WalkedAsync(_play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(252, TestPlay.EastMapId));

        // East, by its coordinates: left, from a cell of the west edge.
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(_play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);

        // Back on the first map, the other way, north.
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(_play, connection, 3));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 539);

        // North, by its id: down.
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(_play, connection, 4));
        Assert.Equal(3, run.Steps);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal(["Bonjour !"], Lines(activity));
    }

    [Fact]
    public async Task AnInterruptionCancelsTheCallTheScriptAwaitsAndTheNextStepGoesOnFromItsState()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        using var activity = Script(Herale);
        var run = bot.StartActivity(activity);

        await TestWalks.SentAsync(_play, connection, 1);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting && bot.IsPaused);

        Assert.Equal(Interruption.Player, run.LastInterruption);
        Assert.Equal(0, run.Steps);

        bot.Resume();

        // The step had turned east before it was cut: called again, it takes the other way, a walk to the north edge.
        var walk = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 2));

        Assert.True(MapGeometry.Neighbour(walk.KeyMovements[^1] & 0xFFF, 6) < 0);

        run.Stop();
        await run.Completion;

        Assert.Equal(["Bonjour !"], Lines(activity));
    }

    [Fact]
    public async Task PcallDoesNotCatchAnInterruption()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            function step(bot)
              local ok = pcall(function() bot:walk(301) end)
              log.info("pcall returned " .. tostring(ok))
              return false
            end
            """);
        var run = bot.StartActivity(activity);

        await TestWalks.SentAsync(_play, connection, 1);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting && bot.IsPaused);

        Assert.Empty(Lines(activity));

        bot.Resume();
        await TestWalks.WalkedAsync(_play, connection, 2);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(_play.Time, run.Completion));
        Assert.Equal(["pcall returned true"], Lines(activity));
    }

    [Fact]
    public async Task ALoopThatNeverWaitsIsStoppedWithTheActivity()
    {
        var (_, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            function step(bot)
              local n = 0
              while true do n = n + 1 end
            end
            """);
        var run = bot.StartActivity(activity);

        await WaitForAsync(() => run.Status is ActivityStatus.Stepping);
        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnActionAnswersWhetherItSucceededAndWhyNot()
    {
        var (_, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            function step(bot)
              local ok, why = bot:changeMap("right")
              log.info(tostring(ok), why)
              return false
            end
            """);

        Assert.Equal(ActivityOutcome.Done, await bot.StartActivity(activity).Completion);
        Assert.Equal(["false\tnoPath"], Lines(activity));
    }

    [Fact]
    public async Task TheBotsFieldsReadItsStateLive()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new InventoryWeightEvent { Weight = 300, MaxWeight = 1_200 });
        connection.Server(new KamasUpdateEvent { Kamas = 42 });
        await bot.ProcessedAsync();

        using var activity = Script("""
            function step(bot)
              local c, m, i = bot.character, bot.map, bot.inventory
              log.info(c.id, c.name, c.level, m.id, m.x, m.y, m.subArea, bot.cell, i.weight, i.maxWeight, i.load, i.kamas, bot.isBusy, bot.unknown)
              return false
            end
            """);

        Assert.Equal(ActivityOutcome.Done, await bot.StartActivity(activity).Completion);
        Assert.Equal(["70001\tCharacter\t200\t191104002\t4\t-18\t440\t300\t300\t1200\t0.25\t42\tfalse\tnil"], Lines(activity));
    }

    [Fact]
    public async Task AFullCharacterIsUnloadedByTheScriptBeforeItsNextStep()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new InventoryWeightEvent { Weight = 1_100, MaxWeight = 1_200 });
        await bot.ProcessedAsync();

        using var activity = Script("""
            local unloaded = false

            function isFull(bot)
              return not unloaded and bot.inventory.load >= 0.9
            end

            function unload(bot)
              unloaded = true
              log.info("unloaded")
              return true
            end

            function step(bot)
              log.info("step")
              return false
            end
            """);

        Assert.Equal(ActivityOutcome.Done, await bot.StartActivity(activity).Completion);
        Assert.Equal(["unloaded", "step"], Lines(activity));
    }

    [Fact]
    public async Task TheScriptSaysWhatAnInterruptionLeadsToAndRecoversTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            local cuts = 0

            function onInterrupted(reason)
              cuts = cuts + 1
              if cuts > 1 then return "stop" end
            end

            function recover(bot, reason)
              log.info("recovered from " .. reason)
            end

            function step(bot)
              bot:walk(301)
            end
            """);
        var run = bot.StartActivity(activity);

        await TestWalks.SentAsync(_play, connection, 1);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting && bot.IsPaused);
        bot.Resume();

        await TestWalks.SentAsync(_play, connection, 2);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal(["recovered from player"], Lines(activity));
    }

    [Fact]
    public async Task AScriptSendsARequestOfTheReadableProtocolAndWaitsForAnEvent()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            function step(bot)
              bot:send("MapMovementRequest", { mapId = 191104002, keyMovements = { 300, 301 } })
              local event = bot:waitFor("KamasUpdateEvent", 5000)
              log.info(event.kamas)
              local none, why = bot:waitFor("KamasUpdateEvent", 10)
              log.info(tostring(none), why)
              return false
            end
            """);
        var run = bot.StartActivity(activity);

        Assert.Equal(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 300, 301 } }, await connection.SentAsync(1));

        await WaitForAsync(() => run.Status is ActivityStatus.Stepping);
        await bot.ProcessedAsync();
        connection.Server(new KamasUpdateEvent { Kamas = 5 });

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(_play.Time, run.Completion));
        Assert.Equal(["5", "nil\ttimeout"], Lines(activity));
    }

    [Fact]
    public async Task AScriptReachesNoFileNorProcess()
    {
        var (_, bot) = await _play.EnterAsync(300);
        using var activity = Script("""
            local reached = {}
            local names = { "io", "dofile", "loadfile", "load", "require", "package", "debug", "collectgarbage", "bot" }
            for _, name in ipairs(names) do
              if _G[name] ~= nil then reached[#reached + 1] = name end
            end
            for _, name in ipairs({ "execute", "exit", "getenv", "remove", "rename", "tmpname", "setlocale" }) do
              if os[name] ~= nil then reached[#reached + 1] = "os." .. name end
            end
            if string.dump ~= nil then reached[#reached + 1] = "string.dump" end
            print("reached: " .. table.concat(reached, ", "))
            print(string.format("%d %s", math.floor(os.time() / os.time()), type(os.clock())))

            function step(bot) return false end
            """);

        Assert.Equal(ActivityOutcome.Done, await bot.StartActivity(activity).Completion);
        Assert.Equal(["reached: ", "1 number"], Lines(activity));
    }

    [Theory]
    [InlineData("function step(bot)\n  local t = nil\n  return t.x\nend", "test.lua:3: attempt to index a nil value (local 't')")]
    [InlineData("function step(bot)\n  error(\"no zaap known\")\nend", "test.lua:2: no zaap known")]
    [InlineData("function step(bot)\n  bot:walk(\"nowhere\")\nend", "test.lua:2: bot:walk: bad argument #1 (whole number expected, got string)")]
    [InlineData("function step(bot)\n  bot:walk(600)\nend", "test.lua:2: bot:walk: bad argument #1 (600 is not between 0 and 559)")]
    [InlineData("function step(bot)\n  bot:send(\"Teleport\", {})\nend", "test.lua:2: bot:send: bad argument #1 (no request of D3B's readable protocol is named Teleport)")]
    [InlineData("function step(bot)\n  bot:send(\"MapMovementRequest\", { cell = 1 })\nend", "test.lua:2: bot:send: MapMovementRequest has no field cell; its fields are keyMovements, mapId, flag3")]
    [InlineData("function step(bot)\n  bot:send(\"MapMovementRequest\", { mapId = 1.5 })\nend", "test.lua:2: bot:send: MapMovementRequest.mapId holds a whole number that fits a Int64, not 1.5")]
    [InlineData("function step(bot)\n  bot.cell = 3\nend", "test.lua:2: bot: the bot's fields are read, not written: cell")]
    [InlineData("function isFull(bot)\n  local ok = bot:walk(301)\n  return ok\nend\nfunction step(bot) end", "test.lua:2: bot:walk: isFull(bot) reads the bot, and never acts")]
    [InlineData("function step(\n  return\nend", "test.lua:2: <name> or '...' expected near 'return'")]
    [InlineData("print('no step')", "test.lua: the script defines no step(bot) function, which the bot calls for each step.")]
    [InlineData("bot:walk(301)", "test.lua:1: attempt to index a nil value (global 'bot')")]
    public async Task AScriptsErrorFailsTheActivityWithItsLine(string source, string message)
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new InventoryWeightEvent { Weight = 1_100, MaxWeight = 1_200 });
        await bot.ProcessedAsync();

        using var activity = Script(source);
        var run = bot.StartActivity(activity);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal(message, Assert.IsType<ScriptException>(run.Failure).Message);
        Assert.Equal(new ScriptLine(_play.Time.GetUtcNow(), ScriptLineKind.Error, message), activity.Console.Lines()[^1]);
    }

    private static ScriptActivity Script(string source)
    {
        return new ScriptActivity(new LuaScript("test.lua", source));
    }

    private static List<string> Lines(ScriptActivity activity)
    {
        return [.. activity.Console.Lines().Select(line => line.Text)];
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The script never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
