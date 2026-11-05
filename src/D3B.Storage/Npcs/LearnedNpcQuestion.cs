namespace D3B.Storage.Npcs;

/// <summary>A question an NPC asked on a map, with the replies it offered (D383).</summary>
public sealed class LearnedNpcQuestion
{
    /// <summary>Gets the map the NPC stood on.</summary>
    public long MapId { get; init; }

    /// <summary>Gets which NPC asked.</summary>
    public int NpcId { get; init; }

    /// <summary>Gets the question's id in the client's tables.</summary>
    public int MessageId { get; init; }

    /// <summary>Gets the ids of the replies it offered, in the order the server gave them, separated by commas; empty when it offered none.</summary>
    public string Replies { get; init; } = string.Empty;

    /// <summary>Gets or sets how many times it opened a dialog.</summary>
    public int Opened { get; set; }

    /// <summary>Gets or sets how many times it was asked with these replies.</summary>
    public int Seen { get; set; }

    /// <summary>Gets when it first was.</summary>
    public DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Gets or sets when it last was.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
