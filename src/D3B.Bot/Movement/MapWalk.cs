using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Movement;

/// <summary>The first behavior: walking to a cell of the character's map (D69).</summary>
/// <remarks>
/// <para>
/// The path is found in the pack's cells (<see cref="CellPathfinder"/>), from the cell the state says the character
/// stands on, and sent as the client sends its own: the path's turning points, each the direction times 4096 plus the
/// cell (D60). The server then shows the walk, or refuses it; the client walks it, confirms once arrived, and the server
/// answers the confirmation: the walk is over. D3B never confirms: the client does (D60).
/// </para>
/// <para>
/// Delays, measured (client.md): the server answered every path within 150 ms, so 3 s; the client confirmed 0.38 to
/// 2.9 s later, a step taking at most 186 ms running and 491 ms walking, and the server answered within 100 ms, so 1 s
/// and 500 ms a step. They only bound a wait that went wrong: the server's answers end the walk.
/// </para>
/// </remarks>
public static class MapWalk
{
    private const int DirectionShift = 12;

    private static readonly TimeSpan _answerDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _arrivalDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _stepDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Walks the bot's character to a cell of its map, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="cell">The cell to walk to.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>How the walk ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<WalkOutcome> WalkAsync(this BotActor bot, int cell, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        CheckCell(cell);

        return bot.RunAsync((context, token) => context.WalkAsync(cell, token), cancellationToken);
    }

    /// <summary>Walks the character to a cell of its map, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cell">The cell to walk to.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the walk ended.</returns>
    public static async Task<WalkOutcome> WalkAsync(this BotContext context, int cell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CheckCell(cell);

        var state = context.State;
        var self = state.Key.CharacterId;

        if (state.MapId is not { } mapId || state.Cell is not { } from || state.IsWalking)
            return WalkOutcome.Busy;

        if (from == cell)
            return WalkOutcome.Arrived;

        if (!context.Maps.TryGet(mapId, out var map) || !CellPathfinder.TryFind(map.Cells.Span, from, cell, out var path))
            return WalkOutcome.NoPath;

        // Both are expected before the request goes, so that no answer can come before the bot looks for it.
        var answer = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is MapMovementEvent movement && movement.ActorId == self || message is MapMovementRefusedEvent));
        var arrival = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is MapMovementConfirmResponse);

        BotLog.Walking(context.Log, self, from, cell, path.Steps);
        await context.SendAsync(Request(mapId, path), cancellationToken).ConfigureAwait(false);

        var outcome = await AnswerAsync(answer, arrival, path, cancellationToken).ConfigureAwait(false);

        BotLog.Walked(context.Log, self, cell, outcome);

        return outcome;
    }

    /// <summary>Gets the request to walk a path, as the client words its own.</summary>
    /// <param name="mapId">The character's map.</param>
    /// <param name="path">The path, from the cell the character stands on.</param>
    /// <returns>The request.</returns>
    public static MapMovementRequest Request(long mapId, CellPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var request = new MapMovementRequest { MapId = mapId };

        request.KeyMovements.AddRange(path.TurningPoints().Select(turn => (turn.Direction << DirectionShift) | turn.Cell));

        return request;
    }

    private static async Task<WalkOutcome> AnswerAsync(GameExpectation answer, GameExpectation arrival, CellPath path, CancellationToken cancellationToken)
    {
        var answered = await answer.WaitAsync(_answerDelay, cancellationToken).ConfigureAwait(false);

        if (answered is null)
            return WalkOutcome.NoAnswer;

        if (answered is MapMovementRefusedEvent)
            return WalkOutcome.Refused;

        var arrived = await arrival.WaitAsync(_arrivalDelay + path.Steps * _stepDelay, cancellationToken).ConfigureAwait(false);

        return arrived is null ? WalkOutcome.NotConfirmed : WalkOutcome.Arrived;
    }

    private static void CheckCell(int cell)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cell);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cell, MapData.CellCount);
    }
}
