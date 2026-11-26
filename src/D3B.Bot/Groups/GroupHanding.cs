using D3B.Bot.Characters;

namespace D3B.Bot.Groups;

/// <summary>A master giving a follower objects it lacks, such as a dungeon's key before the door (D569): the follower whose turn it is and the round, a new one at each exchange.</summary>
/// <param name="Follower">The follower.</param>
/// <param name="Round">The round's number, so that a follower asks once a round.</param>
public sealed record GroupHanding(CharacterKey Follower, int Round);
