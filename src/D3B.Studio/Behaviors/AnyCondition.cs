using System.Collections.Immutable;

namespace D3B.Studio.Behaviors;

/// <summary>Conditions of which one at least holds; none holds as false.</summary>
/// <param name="Conditions">The conditions.</param>
public sealed record AnyCondition(ImmutableArray<Condition> Conditions) : Condition;
