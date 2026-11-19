using D3B.Bot.Characters;

namespace D3B.Bot.Groups;

/// <summary>A master taking a follower's objects at the bank: the follower whose turn it is, the round, a new one at each exchange (D442), and what is given, so that both reckon the same gifts (D459).</summary>
/// <param name="Follower">The follower.</param>
/// <param name="Round">The round's number, so that a follower asks once a round.</param>
/// <param name="Food">Whether the food that only heals is given too, as the master's profile banks all but what is worn.</param>
/// <param name="Left">The follower's stacks an exchange of that unloading left, given no more.</param>
public sealed record GroupCollect(CharacterKey Follower, int Round, bool Food, IReadOnlySet<int> Left);
