using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Inventory;

/// <summary>Using an object of the inventory, one at a time: eating, or opening a resource bag (D148, D153).</summary>
/// <remarks>
/// Measured on client 3.7.12.12: the request names the stack, and the server answers 28 ms later. Food heals: the life's
/// regeneration stops and starts again, the stack's quantity lessens, a text, the characteristics with the life healed and
/// the weight follow. A bag gives what it holds, a text, then the bag's quantity, or its stack deleted once empty, and the
/// weight. At full life the server refuses food with text 225 and uses nothing.
/// </remarks>
public static class ObjectUse
{
    /// <summary>The text the server refuses food with at full life, measured.</summary>
    public const int FullLifeTextId = 225;

    /// <summary>Uses one object of a stack the character carries, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="uid">The stack's own id.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the use ended; once used, the state holds the stack's new quantity and the weight.</returns>
    public static async Task<ObjectUseOutcome> UseObjectAsync(this BotContext context, int uid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        if (!state.Inventory.TryGetValue(uid, out var stack) || stack.IsWorn)
            return ObjectUseOutcome.NotCarried;

        if (state.IsBusy)
            return ObjectUseOutcome.Busy;

        using var action = context.BeginAction();
        var depth = action.Depth;
        var answered = context.Expect((side, message, _) => side is RelayDirection.FromServer && message switch
        {
            ObjectQuantityEvent { Object: { } changed } => changed.Uid == uid,
            ObjectDeletedEvent deleted => deleted.Uid == uid,
            ObjectsDeletedEvent deleted => deleted.Uids.Contains(uid),
            TextInformationEvent text => text.TextId == FullLifeTextId,
            _ => false,
        });

        // The weight comes last of what a use changes.
        var weighed = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is InventoryWeightEvent);

        await context.SendAsync(new ObjectUseRequest { Uid = uid }, cancellationToken).ConfigureAwait(false);

        var outcome = await answered.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) switch
        {
            null => ObjectUseOutcome.NoAnswer,
            TextInformationEvent => ObjectUseOutcome.Refused,
            _ => ObjectUseOutcome.Used,
        };

        if (outcome is ObjectUseOutcome.Used)
            await weighed.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new ObjectUseEntry(at, depth, stack.ItemId, outcome, took));

        return outcome;
    }
}
