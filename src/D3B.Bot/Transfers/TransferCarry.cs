using D3B.Bot.Banks;

namespace D3B.Bot.Transfers;

/// <summary>What one character's transfer carries: the character's own share of each stack, the items taken and not yet put (D563), and the items each way's destination refuses for good (D572).</summary>
/// <remarks>Its stacks are named by their ids, which hold for one game connection only (client-game.md, "The game, banked"); the items refused by their ids in the client's tables, which hold across connections.</remarks>
internal sealed class TransferCarry
{
    /// <summary>Gets or sets the game connection the stacks' ids are of.</summary>
    public string? ConnectionId { get; set; }

    /// <summary>Gets how much of each stack of the bags, by its id, is the character's own.</summary>
    public Dictionary<int, int> Own { get; } = [];

    /// <summary>Gets the items taken out of the source and not yet put in the destination.</summary>
    public SortedSet<int> Carrying { get; } = [];

    /// <summary>Gets the items the destination refused, to put back in the source.</summary>
    public SortedSet<int> GivingBack { get; } = [];

    /// <summary>Gets, for each way, the items its destination refuses whatever the run: an object bound to the account, a type the chest's tab does not take.</summary>
    public Dictionary<TransferDirection, Dictionary<int, StorageMoveOutcome>> Refused { get; } = [];

    /// <summary>Gets whether the transfer carries anything.</summary>
    public bool IsCarrying => Carrying.Count + GivingBack.Count > 0;
}
