using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Data.Maps;
using D3B.Network.Relay;
using Lua;

namespace D3B.Scripting.Api;

/// <summary>The <c>bot</c> a script's hooks receive: the character's state as fields read live, and its actions as methods (D105).</summary>
/// <remarks>
/// <para>
/// A field reads the state as it is when the script reads it, a snapshot the next message the bot sees replaces:
/// <c>bot.map.id</c>, <c>bot.inventory.load</c>. A method acts through the hook's behavior and waits for it to end; it
/// answers as Lua's own functions do, <c>true</c>, or <c>false</c> and why, so that <c>local ok, why = bot:walk(301)</c>
/// reads <c>false, "noPath"</c>. An interruption cancels the method the script waits on, and the hook with it.
/// </para>
/// <para>
/// The protocol is reached through D3B's readable requests alone (<see cref="ProtocolTables"/>): <c>bot:send</c> sends
/// one between the client's frames (D61), <c>bot:waitFor</c> waits for an event or a response, and <c>bot:request</c> does
/// both without missing an answer that comes at once. A request the client does not follow may leave it out of step with
/// the server (D60): the script's author answers for what they send.
/// </para>
/// </remarks>
internal static class BotObject
{
    private const long MaxWaitMilliseconds = 3_600_000;
    private const long DefaultAnswerMilliseconds = 10_000;

    /// <summary>The fields a script reads, by name; nil for a name the bot has not.</summary>
    public static IReadOnlyList<string> Fields { get; } =
        ["character", "map", "cell", "inventory", "jobs", "isBusy", "isWalking", "isUsing", "isInDialog", "isFighting", "isInHavenBag", "isSubscribed"];

    public static LuaTable Create(ScriptBinding binding)
    {
        var bot = new ScriptNamespace("bot", binding)
            .Add("walk", WalkAsync)
            .Add("changeMap", ChangeMapAsync)
            .Add("travel", TravelAsync)
            .Add("use", UseAsync)
            .Add("bank", BankAsync)
            .Add("leaveHavenBag", LeaveHavenBagAsync)
            .Add("wait", WaitAsync)
            .Add("waitIdle", WaitIdleAsync)
            .Add("count", Count)
            .Add("jobLevel", JobLevel)
            .Add("send", SendAsync)
            .Add("waitFor", WaitForAsync)
            .Add("request", RequestAsync);
        var table = bot.Table;

        table.Metatable = new LuaTable
        {
            ["__index"] = new LuaFunction("bot", (context, _) =>
            {
                var call = new ScriptCall(context, null, "bot", binding);
                var key = call.Argument(1);

                return new ValueTask<int>(call.Return(key.TryRead<string>(out var name) ? Field(call, name) : LuaValue.Nil));
            }),
            ["__newindex"] = new LuaFunction("bot", (context, _) =>
            {
                throw new ScriptCall(context, null, "bot", binding).Error($"the bot's fields are read, not written: {context.GetArgument(1)}");
            }),
        };

        return table;
    }

    private static LuaValue Field(ScriptCall call, string name)
    {
        if (!Fields.Contains(name))
            return LuaValue.Nil;

        var state = call.Character;

        return name switch
        {
            "character" => new LuaTable { ["id"] = state.Key.CharacterId, ["name"] = state.Name, ["level"] = Outcomes.Of(state.Level) },
            "map" => Map(call.Binding, state),
            "cell" => Outcomes.Of(state.Cell),
            "inventory" => Inventory(state),
            "jobs" => Jobs(state),
            "isBusy" => state.IsBusy,
            "isWalking" => state.IsWalking,
            "isUsing" => state.IsUsing,
            "isInDialog" => state.IsInDialog,
            "isFighting" => state.IsFighting,
            "isInHavenBag" => state.IsInHavenBag,
            _ => state.IsSubscribedAt(call.Binding.Time.GetUtcNow()),
        };
    }

    private static LuaValue Map(ScriptBinding binding, CharacterState state)
    {
        if (state.MapId is not { } mapId)
            return LuaValue.Nil;

        var map = new LuaTable { ["id"] = mapId, ["subArea"] = Outcomes.Of(state.SubAreaId) };

        if (binding.World?.Atlas.TryGetPosition(mapId, out var position) is true)
        {
            map["x"] = position.X;
            map["y"] = position.Y;
            map["worldMap"] = position.WorldMap;
        }

        return map;
    }

    private static LuaTable Inventory(CharacterState state)
    {
        var items = new LuaTable();
        var index = 1;

        foreach (var stack in state.Inventory.Values)
            items[index++] = new LuaTable { ["uid"] = stack.Uid, ["item"] = stack.ItemId, ["quantity"] = stack.Quantity, ["worn"] = stack.IsWorn };

        return new LuaTable
        {
            ["weight"] = state.Weight,
            ["maxWeight"] = state.MaxWeight,
            ["load"] = state.Load,
            ["kamas"] = state.Kamas,
            ["items"] = items,
        };
    }

    private static LuaTable Jobs(CharacterState state)
    {
        var jobs = new LuaTable();

        foreach (var job in state.Jobs.Values)
            jobs[job.JobId] = new LuaTable { ["id"] = job.JobId, ["level"] = job.Level, ["experience"] = job.Experience };

        return jobs;
    }

    private static async Task<int> WalkAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var cell = call.Integer(0, 0, MapData.CellCount - 1);

        return Outcomes.Return(call, await call.Context.WalkAsync(cell, cancellationToken).ConfigureAwait(false), WalkOutcome.Arrived);
    }

    private static async Task<int> ChangeMapAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var where = call.Argument(0);
        var context = call.Context;
        MapChangeOutcome outcome;

        if (where.TryRead<string>(out var side) && MapExits.TryReadSide(side, out var direction))
            outcome = await MapExits.BySideAsync(context, direction, cancellationToken).ConfigureAwait(false);
        else if (where.TryRead<string>(out var text) && MapExits.TryReadCell(text, out var cell) || where.TryRead<int>(out cell) && cell is >= 0 and < MapData.CellCount)
            outcome = await MapExits.FromCellAsync(context, cell, cancellationToken).ConfigureAwait(false);
        else
            throw call.Error($"bad argument #1 (a side, \"top\", \"bottom\", \"left\" or \"right\", or a map action's cell expected, got {where})");

        return Outcomes.Return(call, outcome, MapChangeOutcome.Changed);
    }

    private static async Task<int> TravelAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var maps = new HashSet<long>();

        if (call.Argument(0).Type is LuaValueType.Table)
        {
            var table = call.Table(0)!;

            for (var index = 1; index <= table.ArrayLength; index++)
            {
                if (!table[index].TryRead<double>(out var id) || id != Math.Floor(id))
                    throw call.Error($"bad argument #1 (the map ids of a list are whole numbers, not {table[index]})");

                maps.Add((long)id);
            }
        }
        else
        {
            maps.Add(call.Integer(0));
        }

        if (maps.Count == 0)
            throw call.Error("bad argument #1 (no map to travel to)");

        return Outcomes.Return(call, await call.Context.TravelAsync(maps, cancellationToken).ConfigureAwait(false), TravelOutcome.Arrived);
    }

    private static async Task<int> UseAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var element = call.Integer(0, 0, int.MaxValue);
        var skill = call.Integer(1, 0, int.MaxValue);

        return Outcomes.Return(call, await call.Context.UseAsync(element, skill, cancellationToken).ConfigureAwait(false), UseOutcome.Used);
    }

    private static async Task<int> BankAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var items = call.Table(0);
        var kamas = call.Integer(1, 0L);
        HashSet<int>? chosen = null;

        if (items is not null)
        {
            chosen = [];

            for (var index = 1; index <= items.ArrayLength; index++)
            {
                if (!items[index].TryRead<int>(out var item))
                    throw call.Error($"bad argument #1 (the items of a list are their ids, whole numbers, not {items[index]})");

                chosen.Add(item);
            }
        }

        if (kamas < 0)
            throw call.Error($"bad argument #2 (kamas to put in are 0 or more, not {kamas})");

        // Every stack not worn by default: what the character carries for the bank, never what it wears.
        var outcome = await call.Context.BankAsync(stack => !stack.IsWorn && (chosen is null || chosen.Contains(stack.ItemId)), kamas, cancellationToken).ConfigureAwait(false);

        return Outcomes.Return(call, outcome, BankOutcome.Done);
    }

    private static async Task<int> LeaveHavenBagAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        return Outcomes.Return(call, await call.Context.LeaveHavenBagAsync(cancellationToken).ConfigureAwait(false), MapChangeOutcome.Changed);
    }

    private static async Task<int> WaitAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var milliseconds = call.Integer(0);

        // A script's longest pause is bounded, so that seconds written as milliseconds do not hold a bot for weeks.
        if (milliseconds is < 0 or > MaxWaitMilliseconds)
            throw call.Error($"bad argument #1 (a wait lies between 0 and {MaxWaitMilliseconds} milliseconds, not {milliseconds})");

        var context = call.Context;

        await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), context.Time, cancellationToken).ConfigureAwait(false);

        return call.Return();
    }

    private static async Task<int> WaitIdleAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var timeout = Timeout(call, 0);

        return call.Return(await call.Context.WaitUntilAsync(state => !state.IsBusy, timeout, cancellationToken).ConfigureAwait(false));
    }

    private static int Count(ScriptCall call)
    {
        var item = call.Integer(0, 0, int.MaxValue);

        return call.Return(call.Character.Inventory.Values.Where(stack => stack.ItemId == item).Sum(stack => (long)stack.Quantity));
    }

    private static int JobLevel(ScriptCall call)
    {
        var job = call.Integer(0, 0, int.MaxValue);

        return call.Return(call.Character.Jobs.TryGetValue(job, out var known) ? known.Level : 0);
    }

    private static async Task<int> SendAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var request = Request(call);

        await call.Context.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return call.Return(true);
    }

    private static async Task<int> WaitForAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var answer = Answer(call, 0);
        var timeout = Timeout(call, 1);
        var expected = call.Context.Expect((side, message, _) => side is RelayDirection.FromServer && message.Descriptor == answer);

        return Answered(call, await expected.WaitAsync(timeout, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<int> RequestAsync(ScriptCall call, CancellationToken cancellationToken)
    {
        var request = Request(call);
        var answer = Answer(call, 2);
        var timeout = Timeout(call, 3);
        var context = call.Context;

        // Expected before it is sent, as every behavior's answer is (D67): an answer that comes at once is not missed.
        var expected = context.Expect((side, message, _) => side is RelayDirection.FromServer && message.Descriptor == answer);

        await context.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return Answered(call, await expected.WaitAsync(timeout, cancellationToken).ConfigureAwait(false));
    }

    private static Google.Protobuf.IMessage Request(ScriptCall call)
    {
        var name = call.Text(0);

        if (!ProtocolTables.TryFindRequest(name, out var descriptor))
            throw call.Error($"bad argument #1 (no request of D3B's readable protocol is named {name})");

        var self = call;

        return ProtocolTables.Build(descriptor, call.Table(1), message => self.Error(message));
    }

    private static Google.Protobuf.Reflection.MessageDescriptor Answer(ScriptCall call, int index)
    {
        var name = call.Text(index);

        return ProtocolTables.TryFindAnswer(name, out var descriptor)
            ? descriptor
            : throw call.Error($"bad argument #{index + 1} (no event or response of D3B's readable protocol is named {name})");
    }

    private static TimeSpan Timeout(ScriptCall call, int index)
    {
        var milliseconds = call.Integer(index, DefaultAnswerMilliseconds);

        if (milliseconds is < 0 or > MaxWaitMilliseconds)
            throw call.Error($"bad argument #{index + 1} (a wait lies between 0 and {MaxWaitMilliseconds} milliseconds, not {milliseconds})");

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static int Answered(ScriptCall call, Google.Protobuf.IMessage? answer)
    {
        return answer is null ? call.Return(LuaValue.Nil, "timeout") : call.Return(ProtocolTables.ToTable(answer));
    }
}
