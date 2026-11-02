namespace D3B.Bot.Behaviors;

/// <summary>A rule once compiled: its event, its conditions and its first action, every one played at once.</summary>
internal sealed class CompiledRule
{
    public required string Id { get; init; }

    public required IEventNode Event { get; init; }

    public required BoundValues EventValues { get; init; }

    public CompiledCondition? When { get; init; }

    public CompiledStep? Entry { get; init; }
}
