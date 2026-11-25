using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>The exchange with a storage the character is in, such as its bank's, from the server's opening it to its end.</summary>
/// <param name="Kamas">The kamas the storage holds; the guild's chest holds none.</param>
/// <param name="IsGuildChest">Whether it is the guild's chest, rather than the character's own in the bank (D147, D153).</param>
/// <remarks>
/// Measured on the owner's captures of 20 to 25 November (D551): the server sends the stacks as the storage opens, each stack
/// put in or taken out in part as it now is, and each stack taken out whole as removed. A list of stacks the client takes at
/// once is told by an event the readable protocol lacks, which only the player's own client sends for.
/// </remarks>
public sealed record OpenStorage(long Kamas, bool IsGuildChest = false)
{
    /// <summary>Gets the stacks the storage holds, by their own ids, which differ from the inventory's; for the guild's chest, those of the tab it opened on.</summary>
    public ImmutableSortedDictionary<int, StorageItem> Stacks { get; init; } = ImmutableSortedDictionary<int, StorageItem>.Empty;
}
