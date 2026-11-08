namespace D3B.Bot.Activities;

/// <summary>A sub-area an activity may rotate through, and what it is worth to the character (<see cref="PlaceRotation"/>, D391).</summary>
/// <param name="SubAreaId">The sub-area.</param>
/// <param name="Score">Its worth: the richest has the most.</param>
public readonly record struct RotationCandidate(int SubAreaId, double Score);
