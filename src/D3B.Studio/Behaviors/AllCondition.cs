using System.Collections.Immutable;

namespace D3B.Studio.Behaviors;

/// <summary>Conditions that all hold; none holds as true.</summary>
/// <param name="Conditions">The conditions.</param>
public sealed record AllCondition(ImmutableArray<Condition> Conditions) : Condition;
