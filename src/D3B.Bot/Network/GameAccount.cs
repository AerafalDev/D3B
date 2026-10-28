namespace D3B.Bot.Network;

/// <summary>The Ankama account a client logged in with, as the connection phase's identification names it.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Nickname">The account's nickname.</param>
/// <param name="Tag">The tag that tells accounts of one nickname apart.</param>
public sealed record GameAccount(long Id, string Nickname, string Tag);
