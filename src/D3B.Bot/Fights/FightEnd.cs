using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>How the fight ended for the character, and what it gained.</summary>
/// <param name="Outcome">The character's outcome: won or lost.</param>
/// <param name="Experience">The experience the fight gave the character.</param>
/// <param name="Kamas">The kamas it gained.</param>
/// <param name="Items">The objects it gained, by item id: how many of each.</param>
public sealed record FightEnd(FightOutcome Outcome, long Experience, long Kamas, ImmutableSortedDictionary<int, int> Items)
{
    /// <summary>Gets the map the character was on as the fight began, not the fight's own (<see cref="FightState.FromMapId"/>).</summary>
    public long? FromMapId { get; init; }

    /// <summary>Gets the highest level of the monsters the character's team fought, 0 when none was shown (D582).</summary>
    public int HighestMonsterLevel { get; init; }
}
