namespace D3B.Bot.Characters;

/// <summary>A fight a member of the character's party is in, as the server told the party (D435).</summary>
/// <param name="MemberId">The member fighting: the leader, in the one fight captured.</param>
/// <param name="FightId">The fight, which a request to join it names.</param>
/// <param name="MapId">The map the fight is on.</param>
public sealed record PartyMemberFight(long MemberId, int FightId, long MapId);
