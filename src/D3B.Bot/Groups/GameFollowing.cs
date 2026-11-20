using D3B.Bot.Characters;

namespace D3B.Bot.Groups;

/// <summary>Whether a group follows by the game's own options rather than by the bot's walks (D476).</summary>
/// <remarks>
/// <para>
/// The owner's choice: every account of the group subscribed, every follower of level 10 or more, which the game's following
/// asks (D435), and the master leading the party, since the game follows the party's leader, whoever it is (D471). Then each
/// follower turns on its three options, joining the leader's fights, ready by itself and following the leader, and the
/// master asks its members to follow it. Else the bot moves its followers, as before (D431).
/// </para>
/// <para>
/// Measured on the owner's game of 2026-11-20: a follower's client walks after its leader by itself, 4 to 6 ms after the
/// server tells it where; the server joins the followers to the leader's fight as it attacks and sets them ready as it is.
/// A map changed while following is not measured: the bot still watches each follower and takes it to its master's map
/// itself when the game does not.
/// </para>
/// </remarks>
internal static class GameFollowing
{
    /// <summary>The level the game's following of the leader asks of a member (D435).</summary>
    public const int Level = 10;

    /// <summary>Tells whether a master's group follows by the game's options.</summary>
    /// <param name="master">The master's state.</param>
    /// <param name="followers">Its followers' states.</param>
    /// <param name="now">The moment, by the bots' clock, which says whether a subscription runs.</param>
    /// <returns>Whether every account is subscribed, every follower of the level, and the master leads its party.</returns>
    public static bool Applies(CharacterState master, IReadOnlyList<CharacterState> followers, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(followers);

        return followers.Count > 0 && master.Party?.LeaderId == master.Key.CharacterId && master.IsSubscribedAt(now)
            && followers.All(follower => follower.IsSubscribedAt(now) && follower.Level >= Level);
    }
}
