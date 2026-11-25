namespace D3B.Bot.Characters;

/// <summary>A stack of objects a storage holds, such as the bank or the guild's chest (D551).</summary>
/// <param name="Uid">The stack's own id in the storage, which a request taking it out names.</param>
/// <param name="ItemId">The item's id in the client's tables (D29).</param>
/// <param name="Quantity">How many objects the stack holds.</param>
public sealed record StorageItem(int Uid, int ItemId, int Quantity);
