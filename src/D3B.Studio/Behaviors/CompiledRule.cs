namespace D3B.Studio.Behaviors;

/// <summary>A rule once compiled: its event, its conditions and its first action, every one played at once.</summary>
internal sealed class CompiledRule
{
    public required string Id { get; init; }

    /// <summary>Gets the rule's name, or its id when it has none, which the trace says.</summary>
    public required string Name { get; init; }

    public required IEventNode Event { get; init; }

    public required BoundValues EventValues { get; init; }

    public CompiledCondition? When { get; init; }

    public CompiledStep? Entry { get; init; }
}
