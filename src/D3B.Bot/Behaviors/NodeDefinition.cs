using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Settings;

namespace D3B.Bot.Behaviors;

/// <summary>A kind of node: its id, what it does, its sentence of typed values, its help and example (D298).</summary>
/// <remarks>
/// A module declares it in C# (<see cref="NodeServiceCollectionExtensions.AddNode{TNode, TValues}"/>), its values a class it
/// describes as an activity's settings, so that they are typed, chosen and never typed in, checked, and stored as JSON as
/// a profile's settings were (D98). Each activity a module registers is a node of its own, its settings its values (D297).
/// </remarks>
public sealed class NodeDefinition
{
    private readonly Func<IServiceProvider, object>? _create;

    /// <summary>Gets the kind's id, unique in the host, which behaviors' files name.</summary>
    public string Id { get; }

    /// <summary>Gets what the node does.</summary>
    public NodeRole Role { get; }

    /// <summary>Gets its sentence, in the app's language, its values' keys between braces, such as <c>Pods at least {Percent} %</c>.</summary>
    public LocalizedText Sentence { get; }

    /// <summary>Gets its short help, if it has one.</summary>
    public LocalizedText? Help { get; }

    /// <summary>Gets an example of it, if it has one.</summary>
    public LocalizedText? Example { get; }

    /// <summary>Gets its values, described as an activity's settings; none for a branch or a variable, whose compiler reads their own.</summary>
    public ActivitySettings Values { get; }

    /// <summary>Gets whether it is an action played at once, which a rule may play.</summary>
    public bool IsInstant { get; }

    /// <summary>Gets the activity it plays, for an activity's node.</summary>
    public ActivityDefinition? Activity { get; }

    internal NodeDefinition(string id, NodeRole role, LocalizedText sentence, LocalizedText? help, LocalizedText? example, ActivitySettings values, bool isInstant, Func<IServiceProvider, object>? create, ActivityDefinition? activity = null)
    {
        Id = id;
        Role = role;
        Sentence = sentence;
        Help = help;
        Example = example;
        Values = values;
        IsInstant = isInstant;
        _create = create;
        Activity = activity;
    }

    internal object Create(IServiceProvider services)
    {
        return _create?.Invoke(services) ?? throw new InvalidOperationException($"The node {Id} has no code of its own.");
    }
}
