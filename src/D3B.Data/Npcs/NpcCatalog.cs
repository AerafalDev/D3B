using MemoryPack;

namespace D3B.Data.Npcs;

/// <summary>The non-player characters, and the texts of their dialogs: what the server never sends, which the studio shows instead of ids (D385).</summary>
/// <remarks>
/// <para>
/// The server names an NPC, a question and a reply by their ids alone (D383); their names and texts are the client's. Each
/// table is sorted by identifier and found by binary search, and names its texts by number, which <see cref="Texts.GameTexts"/> reads.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class NpcCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "npcs/catalog";

    /// <summary>Gets the NPCs, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Npc> Npcs { get; }

    /// <summary>Gets the replies NPCs offer, in ascending order of identifier.</summary>
    public ReadOnlyMemory<NpcText> Replies { get; }

    /// <summary>Gets the questions NPCs ask, in ascending order of identifier.</summary>
    public ReadOnlyMemory<NpcText> Questions { get; }

    /// <summary>Initializes a new instance of the <see cref="NpcCatalog"/> class.</summary>
    /// <param name="npcs">The NPCs, in strictly ascending order of identifier.</param>
    /// <param name="replies">The replies, in strictly ascending order of identifier.</param>
    /// <param name="questions">The questions, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public NpcCatalog(ReadOnlyMemory<Npc> npcs, ReadOnlyMemory<NpcText> replies, ReadOnlyMemory<NpcText> questions)
    {
        Tables.CheckAscending(npcs.Span, static npc => npc.Id, nameof(npcs));
        Tables.CheckAscending(replies.Span, static reply => reply.Id, nameof(replies));
        Tables.CheckAscending(questions.Span, static question => question.Id, nameof(questions));

        Npcs = npcs;
        Replies = replies;
        Questions = questions;
    }

    /// <summary>Finds an NPC.</summary>
    /// <param name="id">The NPC, such as one the server says stands on the map.</param>
    /// <param name="npc">The NPC, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetNpc(int id, out Npc npc)
    {
        return Tables.TryFind(Npcs.Span, id, static npc => npc.Id, out npc);
    }

    /// <summary>Finds the text a reply reads.</summary>
    /// <param name="id">The reply, as the server offers it.</param>
    /// <param name="reply">The reply with its text, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetReply(int id, out NpcText reply)
    {
        return Tables.TryFind(Replies.Span, id, static reply => reply.Id, out reply);
    }

    /// <summary>Finds the text a question reads.</summary>
    /// <param name="id">The question, as the server asks it.</param>
    /// <param name="question">The question with its text, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetQuestion(int id, out NpcText question)
    {
        return Tables.TryFind(Questions.Span, id, static question => question.Id, out question);
    }
}
