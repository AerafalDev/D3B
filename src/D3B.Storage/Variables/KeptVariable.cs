namespace D3B.Storage.Variables;

/// <summary>A variable a character keeps, of a behavior's scope "the character", and its value as JSON (D298).</summary>
public sealed class KeptVariable
{
    /// <summary>Gets the account of the character.</summary>
    public long AccountId { get; init; }

    /// <summary>Gets the character.</summary>
    public long CharacterId { get; init; }

    /// <summary>Gets the variable's name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets or sets its value, as JSON.</summary>
    public string Value { get; set; } = "null";
}
