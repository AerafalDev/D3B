using System.Collections.Immutable;

namespace D3B.Bot.Fights;

/// <summary>A challenge of the character's fight, as the server described it and whether it was won (D461).</summary>
/// <param name="Id">The challenge, as the client's challenges' table numbers it.</param>
/// <param name="Bonus">What winning it adds to the reward the challenges' bonus chose, in percent; 0 when the server gave a result alone.</param>
/// <param name="Targets">The fighters it targets, as the server last named them.</param>
/// <param name="Succeeded">Whether it was won, once the server said; null until then.</param>
/// <remarks>
/// Measured on 3.7.14.18 (2,980 fights): one challenge in every fight, its bonus 45 to 130, its result given once, before
/// the fight's end.
/// </remarks>
public sealed record FightChallenge(int Id, int Bonus, ImmutableArray<FightChallengeTarget> Targets, bool? Succeeded);
