using D3B.Extractor.Texts;

namespace D3B.Extractor.Npcs;

/// <summary>A non-player character, such as a banker, as <c>NpcData</c> holds it: whom a character talks to, and what the dialog offers.</summary>
internal sealed class Npc
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    /// <summary>Gets the actions a character may take on it, ids of <c>npc-actions.json</c>, such as 3 to talk.</summary>
    public required IReadOnlyList<int> Actions { get; init; }

    /// <summary>Gets the questions it may ask, its <c>dialogMessages</c>, in the client's order.</summary>
    public required IReadOnlyList<NpcLine> Questions { get; init; }

    /// <summary>Gets the replies it may offer, its <c>dialogReplies</c>, in the client's order.</summary>
    public required IReadOnlyList<NpcLine> Replies { get; init; }
}
