using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Inventory;

/// <summary>Wearing a stack of the inventory at a position (D565).</summary>
/// <remarks>
/// Measured on the client 3.7.14.18 (D565): the request names the stack, how many of its objects and where; the server
/// answers within 30 ms. The whole stack named moves there; a stack worn there before goes back to 63 first, in the same
/// answer, and a net worn adds its taming spell to the character's.
/// </remarks>
public static class ObjectWear
{
    /// <summary>Wears a whole stack the character carries at a position, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="uid">The stack's own id.</param>
    /// <param name="position">Where to wear it.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the wearing ended; once worn, the state holds the stack there.</returns>
    public static async Task<ObjectWearOutcome> WearObjectAsync(this BotContext context, int uid, int position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        if (!state.Inventory.TryGetValue(uid, out var stack))
            return ObjectWearOutcome.NotCarried;

        if (stack.Position == position)
            return ObjectWearOutcome.Worn;

        if (state.IsBusy)
            return ObjectWearOutcome.Busy;

        var moved = context.Expect((side, _, now) => side is RelayDirection.FromServer && now.Inventory.TryGetValue(uid, out var there) && there.Position == position);

        // The whole stack, so that a net used in a fight leaves the next one worn, the stack lessening by one (D565).
        await context.SendAsync(new ObjectSetPositionRequest { Quantity = stack.Quantity, Uid = uid, Position = position }, cancellationToken).ConfigureAwait(false);

        return await moved.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is null ? ObjectWearOutcome.NoAnswer : ObjectWearOutcome.Worn;
    }
}
