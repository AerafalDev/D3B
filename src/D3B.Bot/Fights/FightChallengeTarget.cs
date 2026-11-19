using System.Collections.Immutable;

namespace D3B.Bot.Fights;

/// <summary>A fighter a challenge targets, and the fighters allowed to strike it when the challenge says (D461).</summary>
/// <param name="FighterId">The fighter targeted.</param>
/// <param name="AttackerIds">The fighters allowed to strike it, empty when the challenge names none.</param>
/// <remarks>Measured: of the 410 targets the server's 430 changes of targets named, 145 came with one fighter allowed to strike them, and none with more.</remarks>
public sealed record FightChallengeTarget(long FighterId, ImmutableArray<long> AttackerIds);
