namespace D3B.Bot.Transfers;

/// <summary>How many of each item the transfer moves (D550).</summary>
public enum TransferQuantity
{
    /// <summary>Every one the source holds.</summary>
    All,

    /// <summary>At most <see cref="TransferSettings.Amount"/> of each item over the whole transfer.</summary>
    AtMost,

    /// <summary>All but <see cref="TransferSettings.Amount"/> of each item, which stay in the source.</summary>
    KeepInSource,
}
