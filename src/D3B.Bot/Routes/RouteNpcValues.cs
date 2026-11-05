namespace D3B.Bot.Routes;

/// <summary>An NPC talked to, and the replies given to it: the n-th to the n-th question (D384).</summary>
public sealed class RouteNpcValues
{
    /// <summary>Gets or sets which NPC, by id in the client's tables, or <see langword="null"/> when none is chosen yet.</summary>
    public int? Npc { get; set; }

    /// <summary>Gets or sets the replies, by id, in the order of the questions; none, the dialog is opened and left.</summary>
    public IReadOnlyList<int> Replies { get; set; } = [];
}
