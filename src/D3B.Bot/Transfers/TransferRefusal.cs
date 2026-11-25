namespace D3B.Bot.Transfers;

/// <summary>What the transfer does with an item the destination refuses, such as one a chest's tab does not take or a full chest (D560).</summary>
public enum TransferRefusal
{
    /// <summary>The item is given up for the run, what the character carries of it put back in the source on its next visit, and the rest goes on.</summary>
    GiveBack,

    /// <summary>The item is given up for the run and stays in the character's bags, and the rest goes on.</summary>
    KeepCarried,

    /// <summary>The transfer stops at once and fails, saying which item was refused, which stays in the character's bags.</summary>
    Stop,
}
