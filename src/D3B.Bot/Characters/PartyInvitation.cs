namespace D3B.Bot.Characters;

/// <summary>An invitation into a party the character received and has not answered (D435).</summary>
/// <param name="PartyId">The party, which the answer names.</param>
/// <param name="FromId">The character that invites, the party's leader.</param>
/// <param name="FromName">That character's name.</param>
public sealed record PartyInvitation(int PartyId, long FromId, string FromName);
