namespace D3B.Bot.Transfers;

/// <summary>Which way the transfer moves objects (D550).</summary>
public enum TransferDirection
{
    /// <summary>From the character's own chest at the bank to its guild's chest.</summary>
    BankToGuildChest,

    /// <summary>From the guild's chest to the character's own chest at the bank.</summary>
    GuildChestToBank,
}
