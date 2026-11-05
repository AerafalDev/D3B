using D3B.Bot.Npcs;

namespace D3B.Storage.Npcs;

/// <summary>What followed a reply given to an NPC, or the action of talking to it (D383).</summary>
public sealed class LearnedNpcOutcome
{
    /// <summary>Gets the map the NPC stood on.</summary>
    public long MapId { get; init; }

    /// <summary>Gets which NPC was talked to.</summary>
    public int NpcId { get; init; }

    /// <summary>Gets the question the reply was given to, or 0 for the action of talking.</summary>
    public int MessageId { get; init; }

    /// <summary>Gets the reply, or 0 for the action of talking.</summary>
    public int ReplyId { get; init; }

    /// <summary>Gets what followed.</summary>
    public NpcOutcomeKind Kind { get; init; }

    /// <summary>Gets the next question's id, the map's id or the exchange's type, as the kind says; 0 for the end of the dialog.</summary>
    public long Target { get; init; }

    /// <summary>Gets or sets how many times it followed.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets when it last did.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
