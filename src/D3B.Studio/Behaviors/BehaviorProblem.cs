namespace D3B.Studio.Behaviors;

/// <summary>Something wrong with a behavior, where it is, and what of it (D302).</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Where">Where, by the ids of its states, rules and nodes, such as <c>harvest/n1</c>.</param>
/// <param name="Detail">What of it, in English, naming the values by their keys.</param>
public sealed record BehaviorProblem(BehaviorProblemKind Kind, string Where, string Detail);
