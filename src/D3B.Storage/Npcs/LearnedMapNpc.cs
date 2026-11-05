namespace D3B.Storage.Npcs;

/// <summary>An NPC the bots saw on a map, as the server placed it (D383).</summary>
public sealed class LearnedMapNpc
{
    /// <summary>Gets the map.</summary>
    public long MapId { get; init; }

    /// <summary>Gets which NPC it is, by id in the client's tables.</summary>
    public int NpcId { get; init; }

    /// <summary>Gets or sets its actor id on the map when last seen.</summary>
    public int ActorId { get; set; }

    /// <summary>Gets or sets the cell it stood on when last seen, or null when the server gave none.</summary>
    public int? Cell { get; set; }

    /// <summary>Gets or sets the way it faced when last seen, 0 to 7, or null when the server gave none.</summary>
    public int? Direction { get; set; }

    /// <summary>Gets or sets how many times a map's information listed it.</summary>
    public int Seen { get; set; }

    /// <summary>Gets when it first did.</summary>
    public DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Gets or sets when it last did.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
