using System.Collections.Immutable;

namespace D3B.Bot.Fights;

/// <summary>A fight's placement cells on a map, the red challengers' and the blue defenders', and how often the bots saw them.</summary>
/// <param name="MapId">The map fought on.</param>
/// <param name="ChallengerCells">The challengers' cells, red, in ascending order.</param>
/// <param name="DefenderCells">The defenders' cells, blue, in ascending order.</param>
/// <param name="Seen">How many fights began with them.</param>
/// <param name="FirstSeen">When the first did.</param>
/// <param name="LastSeen">When the last did.</param>
public sealed record FightPlacement(long MapId, ImmutableArray<int> ChallengerCells, ImmutableArray<int> DefenderCells, int Seen, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);
