namespace D3B.Studio.Behaviors;

/// <summary>A condition a module declared, such as the pods at least at a share.</summary>
/// <param name="Call">The condition's node and values.</param>
public sealed record NodeCondition(NodeCall Call) : Condition;
