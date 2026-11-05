namespace D3B.Bot.Npcs;

/// <summary>What followed a reply given to an NPC, or the action of talking to it, and how often (D383).</summary>
/// <remarks>
/// A reply may lead to more than one outcome, as the portal of Incarnam does with the character's level (D253): each is its
/// own row, counted.
/// </remarks>
/// <param name="MapId">The map the NPC stood on.</param>
/// <param name="NpcId">Which NPC was talked to.</param>
/// <param name="MessageId">The question the reply was given to, or <see cref="KnownNpcs.Talk"/> for the action of talking itself.</param>
/// <param name="ReplyId">The reply, or <see cref="KnownNpcs.Talk"/> for the action of talking itself.</param>
/// <param name="Kind">What followed.</param>
/// <param name="Target">The next question's id, the map's id or the exchange's type, as the kind says; 0 for the end of the dialog.</param>
/// <param name="Count">How many times it followed.</param>
/// <param name="LastSeen">When it last did.</param>
public sealed record KnownNpcOutcome(long MapId, int NpcId, int MessageId, int ReplyId, NpcOutcomeKind Kind, long Target, int Count, DateTimeOffset LastSeen);
