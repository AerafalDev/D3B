namespace D3B.Bot.Characters;

/// <summary>A stack of objects the character carries: an item of its inventory.</summary>
/// <param name="Uid">The stack's own id, which the requests moving it name.</param>
/// <param name="ItemId">The item's id in the client's tables (D29).</param>
/// <param name="Quantity">How many objects the stack holds.</param>
/// <param name="Position">Where the stack is: 63 when it is not worn, as every stack obtained in the captures.</param>
public sealed record InventoryItem(int Uid, int ItemId, int Quantity, int Position);
