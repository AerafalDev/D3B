namespace D3B.Bot.Behaviors;

/// <summary>A condition that does not hold.</summary>
/// <param name="Condition">The condition.</param>
public sealed record NotCondition(Condition Condition) : Condition;
