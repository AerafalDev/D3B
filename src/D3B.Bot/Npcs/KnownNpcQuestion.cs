using System.Collections.Immutable;

namespace D3B.Bot.Npcs;

/// <summary>A question an NPC asked on a map, with the replies it offered, as the bots saw it (D383).</summary>
/// <remarks>
/// The same question may offer other replies with the character, its level or a quest, so each set of replies is its own
/// row (D253). Only ids: the texts are the client's, and the server sends none.
/// </remarks>
/// <param name="MapId">The map the NPC stood on.</param>
/// <param name="NpcId">Which NPC asked.</param>
/// <param name="MessageId">The question's id in the client's tables.</param>
/// <param name="Replies">The replies it offered, by id, in the order the server gave them: none for a question that offers none.</param>
/// <param name="Opened">How many times it was the first question of a dialog.</param>
/// <param name="Seen">How many times it was asked with these replies.</param>
/// <param name="FirstSeen">When it first was.</param>
/// <param name="LastSeen">When it last was.</param>
public sealed record KnownNpcQuestion(long MapId, int NpcId, int MessageId, ImmutableArray<int> Replies, int Opened, int Seen, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);
