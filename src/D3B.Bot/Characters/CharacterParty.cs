using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>The party the character is in, as the server gives it (D435).</summary>
/// <param name="Id">The party's id, which its requests name: a new one at each invitation of a leader that had none.</param>
/// <param name="LeaderId">The leader's character.</param>
/// <param name="Members">The members' characters, the leader among them, in the server's order and then as they joined: the order of the group's chain (D430).</param>
public sealed record CharacterParty(int Id, long LeaderId, ImmutableArray<long> Members)
{
    /// <summary>Gets whether the character follows the party's leader, as the game's own option does (D476): from the server's answer to a request to follow, the client's or D3B's, to its answer to a request to stop or its word that the character stopped.</summary>
    /// <remarks>Not known of a party the server gives a client connecting while in it (D455): it counts as not followed.</remarks>
    public bool FollowsLeader { get; init; }
}
