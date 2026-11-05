using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace D3B.Bot.Npcs;

/// <summary>The NPCs the host's bots saw on the maps they went through, and what they learned of the NPCs' dialogs there (D383).</summary>
/// <remarks>
/// <para>
/// One per host, which <see cref="NpcLearner"/> teaches from every game connection, whoever plays it; D3B's database keeps
/// it from one run to the next. Only what the game says is kept, by the client's ids: the server sends no NPC's name and no
/// text, so those are never here.
/// </para>
/// <para>
/// An NPC is one row by map and NPC, whatever its actor id, which may change from one visit to the next; the actor id, the
/// cell and the direction are those of the last visit. A question is one row by map, NPC, question and set of replies.
/// </para>
/// </remarks>
public sealed class KnownNpcs
{
    /// <summary>The message and reply ids of the outcome of the action of talking to an NPC, which has neither.</summary>
    public const int Talk = 0;

    private readonly ConcurrentDictionary<(long MapId, int NpcId), KnownMapNpc> _npcs = new();
    private readonly ConcurrentDictionary<(long MapId, int NpcId, int MessageId, string Replies), KnownNpcQuestion> _questions = new();
    private readonly ConcurrentDictionary<(long MapId, int NpcId, int MessageId, int ReplyId, NpcOutcomeKind Kind, long Target), KnownNpcOutcome> _outcomes = new();

    /// <summary>Raised when a map's information listed an NPC, with that NPC as it now stands.</summary>
    /// <remarks>Raised on the thread that saw it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<KnownMapNpc>? NpcSeen;

    /// <summary>Raised when an NPC asked a question, with that question as it now stands.</summary>
    /// <remarks>Raised on the thread that saw it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<KnownNpcQuestion>? QuestionSeen;

    /// <summary>Raised when something followed a reply, with that outcome as it now stands.</summary>
    /// <remarks>Raised on the thread that saw it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<KnownNpcOutcome>? OutcomeSeen;

    /// <summary>Gets how many NPCs are known, on every map.</summary>
    public int NpcCount => _npcs.Count;

    /// <summary>Gets how many questions are known.</summary>
    public int QuestionCount => _questions.Count;

    /// <summary>Gets how many outcomes are known.</summary>
    public int OutcomeCount => _outcomes.Count;

    /// <summary>Writes a set of replies as the key of a question: the order the server gave them is kept.</summary>
    /// <param name="replies">The replies.</param>
    /// <returns>Their ids, separated by commas.</returns>
    public static string Key(ImmutableArray<int> replies)
    {
        return replies.IsDefaultOrEmpty ? string.Empty : string.Join(',', replies);
    }

    /// <summary>Takes back an NPC seen before, such as what the database kept, without raising <see cref="NpcSeen"/>.</summary>
    /// <param name="npc">The NPC.</param>
    public void Remember(KnownMapNpc npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        _npcs[(npc.MapId, npc.NpcId)] = npc;
    }

    /// <summary>Takes back a question asked before, without raising <see cref="QuestionSeen"/>.</summary>
    /// <param name="question">The question.</param>
    public void Remember(KnownNpcQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        _questions[(question.MapId, question.NpcId, question.MessageId, Key(question.Replies))] = question;
    }

    /// <summary>Takes back an outcome seen before, without raising <see cref="OutcomeSeen"/>.</summary>
    /// <param name="outcome">The outcome.</param>
    public void Remember(KnownNpcOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        _outcomes[(outcome.MapId, outcome.NpcId, outcome.MessageId, outcome.ReplyId, outcome.Kind, outcome.Target)] = outcome;
    }

    /// <summary>Gets the NPCs known on a map, with what is known of their dialogs there.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Its NPCs, in ascending order of NPC.</returns>
    public ImmutableArray<KnownNpc> OnMap(long mapId)
    {
        var questions = _questions.Values.Where(question => question.MapId == mapId).ToLookup(question => question.NpcId);
        var outcomes = _outcomes.Values.Where(outcome => outcome.MapId == mapId).ToLookup(outcome => outcome.NpcId);

        return
        [
            .. _npcs.Values
                .Where(npc => npc.MapId == mapId)
                .OrderBy(npc => npc.NpcId)
                .Select(npc => new KnownNpc(
                    npc,
                    [.. questions[npc.NpcId].OrderByDescending(question => question.Opened).ThenByDescending(question => question.Seen).ThenBy(question => question.MessageId)],
                    [.. outcomes[npc.NpcId].OrderBy(outcome => outcome.MessageId).ThenBy(outcome => outcome.ReplyId).ThenByDescending(outcome => outcome.Count)])),
        ];
    }

    /// <summary>Counts a map's information listing an NPC.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="actorId">The NPC's actor id on the map.</param>
    /// <param name="npcId">Which NPC it is.</param>
    /// <param name="cell">The cell it stands on, when the server gave one.</param>
    /// <param name="direction">The way it faces, when the server gave one.</param>
    /// <param name="at">When.</param>
    /// <returns>The NPC as it now stands.</returns>
    public KnownMapNpc Saw(long mapId, int actorId, int npcId, int? cell, int? direction, DateTimeOffset at)
    {
        var npc = _npcs.AddOrUpdate(
            (mapId, npcId),
            _ => new KnownMapNpc(mapId, npcId, actorId, cell, direction, 1, at, at),
            (_, before) => before with
            {
                ActorId = actorId,
                Cell = cell ?? before.Cell,
                Direction = direction ?? before.Direction,
                Seen = before.Seen + 1,
                LastSeen = Later(before.LastSeen, at),
            });

        NpcSeen?.Invoke(npc);

        return npc;
    }

    /// <summary>Counts a question an NPC asked.</summary>
    /// <param name="mapId">The map the NPC stands on.</param>
    /// <param name="npcId">Which NPC asked.</param>
    /// <param name="messageId">The question's id.</param>
    /// <param name="replies">The replies it offers.</param>
    /// <param name="opened">Whether it opened the dialog.</param>
    /// <param name="at">When.</param>
    /// <returns>The question as it now stands.</returns>
    public KnownNpcQuestion Asked(long mapId, int npcId, int messageId, ImmutableArray<int> replies, bool opened, DateTimeOffset at)
    {
        ImmutableArray<int> offered = replies.IsDefault ? [] : replies;
        var opens = opened ? 1 : 0;
        var question = _questions.AddOrUpdate(
            (mapId, npcId, messageId, Key(offered)),
            _ => new KnownNpcQuestion(mapId, npcId, messageId, offered, opens, 1, at, at),
            (_, before) => before with { Opened = before.Opened + opens, Seen = before.Seen + 1, LastSeen = Later(before.LastSeen, at) });

        QuestionSeen?.Invoke(question);

        return question;
    }

    /// <summary>Counts what followed a reply, or the action of talking.</summary>
    /// <param name="mapId">The map the NPC stands on.</param>
    /// <param name="npcId">Which NPC was talked to.</param>
    /// <param name="messageId">The question the reply was given to, or <see cref="Talk"/>.</param>
    /// <param name="replyId">The reply, or <see cref="Talk"/>.</param>
    /// <param name="kind">What followed.</param>
    /// <param name="target">The next question, the map or the exchange's type; 0 for the end of the dialog.</param>
    /// <param name="at">When.</param>
    /// <returns>The outcome as it now stands.</returns>
    public KnownNpcOutcome Answered(long mapId, int npcId, int messageId, int replyId, NpcOutcomeKind kind, long target, DateTimeOffset at)
    {
        var outcome = _outcomes.AddOrUpdate(
            (mapId, npcId, messageId, replyId, kind, target),
            _ => new KnownNpcOutcome(mapId, npcId, messageId, replyId, kind, target, 1, at),
            (_, before) => before with { Count = before.Count + 1, LastSeen = Later(before.LastSeen, at) });

        OutcomeSeen?.Invoke(outcome);

        return outcome;
    }

    private static DateTimeOffset Later(DateTimeOffset before, DateTimeOffset at)
    {
        return at > before ? at : before;
    }
}
