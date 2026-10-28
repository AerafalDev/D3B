using D3B.Bot.Interactives;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Movement;

/// <summary>Changing maps: one step of a path through the world (D73).</summary>
/// <remarks>
/// <para>
/// Measured (client.md): a scroll is left from a cell of the map's edge. The client walks there, and once the server
/// answered its confirmation, asks for the neighbour, 4 to 6 ms later; the server names the new map 28 to 29 ms after.
/// A map action is left by walking onto its cell, or one next to it: once the walk is confirmed, the server names the new
/// map, then answers the confirmation. Either way, the client then asks what is on the new map, 0.6 to 1.3 s later, and
/// the server answers within 101 ms, placing the character. D3B sends the walk and the request for the neighbour, never
/// the request for the map's information, which the client sends itself (D60). A door is used from a cell next to its
/// element (<see cref="ElementUse"/>): the server starts the use, with no duration, and names the new map in the same
/// millisecond; the client then asks what is on it, as after a scroll.
/// </para>
/// <para>
/// The server answers nothing to a request for the neighbour it refuses: from a cell off its edge, for another map, or
/// once the character is on another map. The change then ends after its delay, 3 s for the new map; 10 s bound the wait
/// for what is on it. It refuses a door's use from too far with an <see cref="InteractiveUseErrorEvent"/>.
/// </para>
/// <para>
/// The change is over once the character is placed. The client then draws the new map, which the bot's next request waits
/// for (<see cref="BotContext.SendAsync"/>, D77).
/// </para>
/// <para>
/// A teleport goes from where the character stands to the zaap of a map it knows (D83): the server names the new map 28
/// ms after the request, with the kamas paid and the dialog over, and the client asks what is on it, 0.7 to 1.4 s later,
/// as after a scroll. It refuses one with an error text, 28 ms after, such as from a map no one teleports from, during a
/// walk or a dialog, or to a map the character knows no zaap on; one it cannot pay is not measured. Leaving the haven bag
/// is the request entering it, which takes the character back to the map it entered it from (D83).
/// </para>
/// </remarks>
public static class MapChange
{
    private static readonly TimeSpan _answerDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _placedDelay = TimeSpan.FromSeconds(10);

    /// <summary>Follows one step of a path through the world, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="step">The step, leaving the vertex the character stands in.</param>
    /// <param name="cancellationToken">Stops the change.</param>
    /// <returns>How the change ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<MapChangeOutcome> ChangeMapAsync(this BotActor bot, WorldStep step, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        return bot.RunAsync((context, token) => context.ChangeMapAsync(step, WorldTravel.RulesOf(context), token), cancellationToken);
    }

    /// <summary>Follows one step of a path through the world, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="step">The step, leaving the vertex the character stands in.</param>
    /// <param name="rules">What the character may follow, which chooses the cell the map is left from.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the change ended.</returns>
    public static async Task<MapChangeOutcome> ChangeMapAsync(this BotContext context, WorldStep step, WorldPathRules rules, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);

        var state = context.State;
        var world = context.World;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || state.IsWalking || state.IsUsing || state.IsInDialog)
            return MapChangeOutcome.Busy;

        if (!context.Maps.TryGet(mapId, out var map) || !world.TryGetVertex(map, cell, out var vertex) || vertex != step.From
            || step.Transition.MapId is < int.MinValue or > int.MaxValue)
        {
            return MapChangeOutcome.NoPath;
        }

        var destination = world.Graph.Vertices.Span[step.To].MapId;
        MapChangeOutcome outcome;

        if (step.IsTeleport)
        {
            BotLog.Teleporting(context.Log, state.Key.CharacterId, mapId, destination);
            outcome = await TeleportAsync(context, mapId, destination, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var exits = world.ExitCells(map, step, rules);

            if (!CellPathfinder.TryFindNearest(map.Cells.Span, cell, [.. exits], out var path))
                return MapChangeOutcome.NoPath;

            BotLog.ChangingMap(context.Log, state.Key.CharacterId, mapId, destination, step.Transition.Type, path.End);

            outcome = step.Transition.Type switch
            {
                WorldTransitionType.MapAction => await StepOnAsync(context, map, path, cancellationToken).ConfigureAwait(false),
                WorldTransitionType.Interactive => await UseDoorAsync(context, mapId, path, step.Transition, cancellationToken).ConfigureAwait(false),
                _ => await ScrollAsync(context, mapId, path, (int)step.Transition.MapId, cancellationToken).ConfigureAwait(false),
            };
        }

        if (outcome is MapChangeOutcome.Changed && context.State.MapId != destination)
            outcome = MapChangeOutcome.Elsewhere;

        BotLog.ChangedMap(context.Log, state.Key.CharacterId, context.State.MapId ?? mapId, outcome);

        return outcome;
    }

    /// <summary>Leaves the haven bag the character is in, back to the map it entered it from, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the change ended: <see cref="MapChangeOutcome.NoPath"/> when the character is in no haven bag, or in another's, whose leaving is not measured.</returns>
    public static async Task<MapChangeOutcome> LeaveHavenBagAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;
        var self = state.Key.CharacterId;

        if (state.MapId is not { } mapId || state.Cell is null || state.IsWalking || state.IsUsing || state.IsInDialog)
            return MapChangeOutcome.Busy;

        if (state.HavenBagOwnerId != self)
            return MapChangeOutcome.NoPath;

        BotLog.LeavingHavenBag(context.Log, self, self);

        var (current, placed) = ExpectNewMap(context, mapId);

        await context.SendAsync(new HavenBagEnterRequest { OwnerId = self }, cancellationToken).ConfigureAwait(false);

        var outcome = await NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false);

        BotLog.ChangedMap(context.Log, self, context.State.MapId ?? mapId, outcome);

        return outcome;
    }

    private static (GameExpectation Current, GameExpectation Placed) ExpectNewMap(BotContext context, long from)
    {
        var current = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is MapCurrentEvent);
        var placed = context.Expect((side, message, state) => side is RelayDirection.FromServer
            && message is MapInformationEvent information && state.MapId is { } map && map != from && information.MapId == map && state.Cell is not null);

        return (current, placed);
    }

    private static async Task<MapChangeOutcome> ScrollAsync(BotContext context, long mapId, CellPath path, int neighbour, CancellationToken cancellationToken)
    {
        // Expected before the walk: a walk onto a cell of the edge may end on a map action's, which moves the character as
        // its walk is confirmed (client.md). The request for the neighbour, then, would go unanswered.
        var (current, placed) = ExpectNewMap(context, mapId);

        if (path.Steps > 0 && await context.WalkAsync(path.End, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived && context.State.MapId == mapId)
            return MapChangeOutcome.NotWalked;

        if (context.State.MapId == mapId)
            await context.SendAsync(new MapChangeRequest { MapId = neighbour }, cancellationToken).ConfigureAwait(false);

        return await NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MapChangeOutcome> StepOnAsync(BotContext context, MapData map, CellPath path, CancellationToken cancellationToken)
    {
        var cells = map.Cells.Span;
        var (current, placed) = ExpectNewMap(context, map.Id);

        // Standing on the cell moves nothing: the server moves the character once a walk onto it is over, so it steps off
        // first, onto a neighbour from which it can come back.
        if (path.Steps is 0)
        {
            var neighbours = Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(path.End, direction)).Where(cell => cell >= 0).ToArray();

            if (!CellPathfinder.TryFindNearest(cells, path.End, neighbours, out var off) || !CellPathfinder.TryFind(cells, off.End, path.End, out _))
                return MapChangeOutcome.NoPath;

            if (await context.WalkAsync(off.End, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived)
                return MapChangeOutcome.NotWalked;
        }

        var walk = context.State.MapId == map.Id ? await context.WalkAsync(path.End, cancellationToken).ConfigureAwait(false) : WalkOutcome.Arrived;

        if (walk is not WalkOutcome.Arrived && context.State.MapId == map.Id)
            return MapChangeOutcome.NotWalked;

        return await NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MapChangeOutcome> UseDoorAsync(BotContext context, long mapId, CellPath path, WorldTransition door, CancellationToken cancellationToken)
    {
        // The server names the door's skill on its element in the map's information: without it, nothing can be asked.
        if (!context.State.Elements.TryGetValue(door.ElementId, out var element) || !element.TryGetEnabled(door.SkillId, out var skill))
            return MapChangeOutcome.NoPath;

        var (current, placed) = ExpectNewMap(context, mapId);

        if (path.Steps > 0 && await context.WalkAsync(path.End, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived && context.State.MapId == mapId)
            return MapChangeOutcome.NotWalked;

        if (context.State.MapId == mapId)
        {
            var answer = context.Expect((side, message, _) => side is RelayDirection.FromServer
                && (message is MapCurrentEvent || message is InteractiveUseErrorEvent error && error.ElementId == door.ElementId));

            await context.SendAsync(ElementUse.Request(door.ElementId, skill), cancellationToken).ConfigureAwait(false);

            if (await answer.WaitAsync(_answerDelay, cancellationToken).ConfigureAwait(false) is InteractiveUseErrorEvent)
                return MapChangeOutcome.Refused;
        }

        return await NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MapChangeOutcome> TeleportAsync(BotContext context, long mapId, long zaap, CancellationToken cancellationToken)
    {
        var (current, placed) = ExpectNewMap(context, mapId);
        var answer = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is MapCurrentEvent || message is TextInformationEvent { Type: TextInformationType.Error }));

        await context.SendAsync(new TeleportRequest { MapId = zaap, Source = TeleporterType.Map }, cancellationToken).ConfigureAwait(false);

        return await answer.WaitAsync(_answerDelay, cancellationToken).ConfigureAwait(false) switch
        {
            null => MapChangeOutcome.NoAnswer,
            TextInformationEvent => MapChangeOutcome.Refused,
            _ => await NewMapAsync(context, current, placed, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<MapChangeOutcome> NewMapAsync(BotContext context, GameExpectation current, GameExpectation placed, CancellationToken cancellationToken)
    {
        if (await current.WaitAsync(_answerDelay, cancellationToken).ConfigureAwait(false) is null)
            return MapChangeOutcome.NoAnswer;

        return await placed.WaitAsync(_placedDelay, cancellationToken).ConfigureAwait(false) is null ? MapChangeOutcome.NotPlaced : MapChangeOutcome.Changed;
    }
}
