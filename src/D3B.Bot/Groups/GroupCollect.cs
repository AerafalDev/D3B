using D3B.Bot.Characters;

namespace D3B.Bot.Groups;

/// <summary>A master taking a follower's objects at the bank: the follower whose turn it is, and the round, a new one at each exchange (D442).</summary>
/// <param name="Follower">The follower.</param>
/// <param name="Round">The round's number, so that a follower asks once a round.</param>
public sealed record GroupCollect(CharacterKey Follower, int Round);
