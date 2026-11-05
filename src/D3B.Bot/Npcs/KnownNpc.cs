using System.Collections.Immutable;

namespace D3B.Bot.Npcs;

/// <summary>An NPC known on a map with what the bots learned of its dialogs there (D383).</summary>
/// <param name="Npc">Where it stands.</param>
/// <param name="Questions">The questions it asked, the most opened first.</param>
/// <param name="Outcomes">What followed its replies, and the action of talking to it.</param>
public sealed record KnownNpc(KnownMapNpc Npc, ImmutableArray<KnownNpcQuestion> Questions, ImmutableArray<KnownNpcOutcome> Outcomes);
