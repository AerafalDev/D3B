using D3B.Bot.Banks;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Groups;

/// <summary>A master giving a follower some of its stacks by an exchange, the group's bank's the other way (D442, D569).</summary>
/// <remarks>
/// The follower asks the exchange as at the bank, once a round (<see cref="GroupDirectory.Hand"/>), so that the exchange measured
/// in D442 is the same, its side who asks included; the master accepts after the client's display time, moves its stacks in,
/// each by a move, the only request that names a quantity (D482), says it is ready once the server can take it, 3.5 s after the
/// last change (D458), and the follower says it is ready after it, which concludes. Not measured: a master moving objects into
/// an exchange its follower asked, which the protocol lets either side do.
/// </remarks>
internal static class GroupHand
{
    // As the master waits at the bank for its follower's asking, then for its readiness (D442).
    private static readonly TimeSpan _exchangeWait = TimeSpan.FromSeconds(30);

    // Measured on the owner's test of 2026-11-19 (D458): the server took no readiness sent 0.5 s after the exchange's last change.
    private static readonly TimeSpan _readyAfterChange = TimeSpan.FromSeconds(3.5);

    /// <summary>Gives a follower stacks, or parts of them, by one exchange.</summary>
    /// <param name="context">The master's behavior context, on its follower's map.</param>
    /// <param name="groups">The host's groups, which tell the follower to ask.</param>
    /// <param name="follower">The follower.</param>
    /// <param name="stacks">Each of the master's stacks and the quantity given.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>Whether the exchange concluded.</returns>
    public static async Task<bool> GiveAsync(BotContext context, GroupDirectory groups, BotActor follower, IReadOnlyList<(int Uid, int Quantity)> stacks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(follower);
        ArgumentNullException.ThrowIfNull(stacks);

        var id = follower.Key.CharacterId;

        using var handing = groups.Hand(context.Actor.Key, follower.Key);

        if (!await context.WaitUntilAsync(state => state.Exchange is { IsOpen: false } asked && asked.SourceId == id, _exchangeWait, cancellationToken).ConfigureAwait(false))
            return false;

        // The client shows the asking first, as it does a map before a walk (D77).
        await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
        await context.SendAsync(new ExchangeAcceptRequest(), cancellationToken).ConfigureAwait(false);

        if (!await context.WaitUntilAsync(state => state.Exchange is { IsOpen: true }, context.Delays.Answer, cancellationToken).ConfigureAwait(false))
            return await LeftAsync(context, cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < stacks.Count; index++)
        {
            if (index > 0)
                await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            if (context.State.Exchange is not { IsOpen: true } open)
                return false;

            var (uid, quantity) = stacks[index];
            var changes = open.Changes;

            await context.SendAsync(new ExchangeObjectMoveRequest { Uid = uid, Quantity = quantity }, cancellationToken).ConfigureAwait(false);
            await context.WaitUntilAsync(state => state.Exchange is not { IsOpen: true } now || now.Changes > changes, context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }

        // Until 3.5 s passed with no change told, as the server takes no readiness sooner (D458).
        for (var seen = context.State.Exchange?.Changes ?? 0; context.State.Exchange is { IsOpen: true }; seen = context.State.Exchange?.Changes ?? seen)
        {
            if (!await context.WaitUntilAsync(state => state.Exchange is not { IsOpen: true } now || now.Changes != seen, _readyAfterChange, cancellationToken).ConfigureAwait(false))
                break;
        }

        // A move the server took nothing of gives the follower nothing: the exchange is left rather than concluded empty.
        if (context.State.Exchange is not { IsOpen: true, Changes: > 0 } moved)
            return await LeftAsync(context, cancellationToken).ConfigureAwait(false);

        await context.SendAsync(new ExchangeReadyRequest { Step = moved.Changes, Ready = true }, cancellationToken).ConfigureAwait(false);

        // The follower's readiness after the master's concludes it.
        if (!await context.WaitUntilAsync(state => state.Exchange is null, _exchangeWait, cancellationToken).ConfigureAwait(false))
            return await LeftAsync(context, cancellationToken).ConfigureAwait(false);

        GroupLog.Handed(context.Log, context.Actor.Key.CharacterId, id, stacks.Count);

        return true;
    }

    private static async Task<bool> LeftAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (context.State.Exchange is { IsOpen: true })
            await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);

        return false;
    }
}
