using System.Collections.Frozen;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Banks;

/// <summary>Moving one stack, or part of it, into or out of the open storage, the bank's or the guild chest's (D551).</summary>
/// <remarks>
/// <para>
/// Measured on the owner's captures of 20 to 25 November, client 3.7.14.18 (client-game.md, "The game, transferred"): the
/// client takes out of the bank and of the chest by <see cref="ExchangeObjectMoveRequest"/>, the storage's stack named with a
/// negative quantity, and puts in by the inventory's stack with a positive one. The server answers 28 ms later, taking out with
/// the inventory's stack, then the storage's, changed or removed, then the weight; putting in with the storage's stack, then the
/// inventory's, then the weight. A take that does not fit in the pods is refused with text 285, nothing moved; an object bound to
/// the account with 356; the chest's rights and its tabs' types with their own texts, which the client's information messages
/// name. The bank answers nothing to a stack it does not take (D80).
/// </para>
/// <para>
/// The client also takes a list of stacks, or all, at once, which the server cuts short to what the pods hold with text 266:
/// those requests are not in the readable protocol, so a stack goes at a time.
/// </para>
/// </remarks>
public static class StorageMoves
{
    /// <summary>The text a take that does not fit in the character's pods is refused with.</summary>
    public const int TooHeavyTextId = 285;

    /// <summary>The text a transfer the destination cannot hold whole is cut short with.</summary>
    public const int IncompleteTextId = 266;

    /// <summary>The text of a rank without the right to open the chest's tab.</summary>
    public const int NoConsultRightTextId = 654;

    /// <summary>The text of a rank without the right to put objects in the chest's tab.</summary>
    public const int NoDepositRightTextId = 655;

    /// <summary>The text of a rank without the right to take objects out of the chest's tab.</summary>
    public const int NoWithdrawRightTextId = 656;

    /// <summary>The text of a chest's tab that does not take a type of object.</summary>
    public const int TypeRefusedTextId = 661;

    // No guild (659), a guild younger than 30 days (666), no access to the chest (711): the client's information messages.
    private static readonly FrozenSet<int> _noRight = FrozenSet.Create(NoConsultRightTextId, NoDepositRightTextId, NoWithdrawRightTextId, 659, 666, 711);

    /// <summary>Takes objects of a stack of the open storage into the inventory.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="uid">The storage's stack, by its own id there.</param>
    /// <param name="quantity">How many to take, at most what the stack holds.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the move ended; <see cref="StorageMoveOutcome.Busy"/> when no storage is open or it lacks the stack.</returns>
    public static async Task<StorageMoveOutcome> TakeFromStorageAsync(this BotContext context, int uid, int quantity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (context.State.Storage is not { } storage || !storage.Stacks.TryGetValue(uid, out var stack))
            return StorageMoveOutcome.Busy;

        var answered = context.Expect((side, message, _) => side is RelayDirection.FromServer && message switch
        {
            StorageObjectUpdateEvent { Object.Item: { } changed } => changed.Uid == uid,
            StorageObjectRemovedEvent removed => removed.Uid == uid,
            TextInformationEvent { Type: TextInformationType.Error } => true,
            _ => false,
        });
        var weight = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is InventoryWeightEvent);

        await context.SendAsync(new ExchangeObjectMoveRequest { Uid = uid, Quantity = -Math.Min(quantity, stack.Quantity) }, cancellationToken).ConfigureAwait(false);

        return await AnsweredAsync(context, answered, weight, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Puts objects of a stack of the inventory into the open storage.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="uid">The inventory's stack, by its own id.</param>
    /// <param name="quantity">How many to put in, at most what the stack holds.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the move ended; <see cref="StorageMoveOutcome.Busy"/> when no storage is open, or the character does not carry the stack or wears it.</returns>
    public static async Task<StorageMoveOutcome> PutInStorageAsync(this BotContext context, int uid, int quantity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var state = context.State;

        if (state.Storage is null || !state.Inventory.TryGetValue(uid, out var stack) || stack.IsWorn)
            return StorageMoveOutcome.Busy;

        var answered = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && message is StorageObjectUpdateEvent or StorageObjectsUpdateEvent or TextInformationEvent { Type: TextInformationType.Error });
        var weight = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is InventoryWeightEvent);

        await context.SendAsync(new ExchangeObjectMoveRequest { Uid = uid, Quantity = Math.Min(quantity, stack.Quantity) }, cancellationToken).ConfigureAwait(false);

        return await AnsweredAsync(context, answered, weight, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells what a text of the error kind the server answers a move with says.</summary>
    /// <param name="textId">The text's id, as <see cref="TextInformationEvent"/> names it.</param>
    /// <returns>The refusal it names; <see cref="StorageMoveOutcome.Refused"/> for a text not measured.</returns>
    public static StorageMoveOutcome RefusalOf(int textId)
    {
        return textId switch
        {
            TooHeavyTextId or IncompleteTextId => StorageMoveOutcome.TooHeavy,
            GuildChest.RefusedTextId => StorageMoveOutcome.NotExchangeable,
            TypeRefusedTextId => StorageMoveOutcome.TypeRefused,
            _ when _noRight.Contains(textId) => StorageMoveOutcome.NoRight,
            _ => StorageMoveOutcome.Refused,
        };
    }

    private static async Task<StorageMoveOutcome> AnsweredAsync(BotContext context, GameExpectation answered, GameExpectation weight, CancellationToken cancellationToken)
    {
        switch (await answered.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false))
        {
            case null:
                return StorageMoveOutcome.NoAnswer;
            case TextInformationEvent text:
                return RefusalOf(text.TextId);
        }

        // The weight comes last of the move's answers, in the same millisecond: the inventory is as the server left it once it came.
        await weight.WaitAsync(context.Delays.BankFollow, cancellationToken).ConfigureAwait(false);

        return StorageMoveOutcome.Moved;
    }
}
