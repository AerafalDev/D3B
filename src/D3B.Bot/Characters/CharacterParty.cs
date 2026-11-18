using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>The party the character is in, as the server gives it (D435).</summary>
/// <param name="Id">The party's id, which its requests name: a new one at each invitation of a leader that had none.</param>
/// <param name="LeaderId">The leader's character.</param>
/// <param name="Members">The members' characters, the leader among them, in the server's order and then as they joined: the order of the group's chain (D430).</param>
public sealed record CharacterParty(int Id, long LeaderId, ImmutableArray<long> Members);
