namespace D3B.Storage.Harvests;

/// <summary>What a map holds, as the bots saw it: its elements that gather a resource, none when it holds none (D584).</summary>
public sealed class LearnedSighting
{
    /// <summary>Gets the map.</summary>
    public long MapId { get; init; }

    /// <summary>Gets or sets when a bot saw it holding these elements, as last written: a sighting that finds the same writes nothing.</summary>
    public DateTimeOffset SeenAt { get; set; }

    /// <summary>Gets or sets its elements that gather a resource, ordered by element then skill.</summary>
    public List<KeptElement> Elements { get; set; } = [];
}
