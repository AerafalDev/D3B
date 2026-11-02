namespace D3B.Storage.Fights;

/// <summary>A fight's placement cells on a map, as the bots saw a fight begin with them (9f3).</summary>
public sealed class LearnedPlacement
{
    /// <summary>Gets the map fought on.</summary>
    public long MapId { get; init; }

    /// <summary>Gets the challengers' cells, red, in ascending order and separated by commas.</summary>
    public string ChallengerCells { get; init; } = string.Empty;

    /// <summary>Gets the defenders' cells, blue, in ascending order and separated by commas.</summary>
    public string DefenderCells { get; init; } = string.Empty;

    /// <summary>Gets or sets how many fights began with them.</summary>
    public int Seen { get; set; }

    /// <summary>Gets when the first did.</summary>
    public DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Gets or sets when the last did.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
