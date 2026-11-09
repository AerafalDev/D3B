using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Activities;
using D3B.Bot.Settings;

namespace D3B.Studio.Behaviors;

/// <summary>The kinds of nodes behaviors use: D3B's own, those the modules declared, and one per activity (D297, D298).</summary>
/// <remarks>
/// Registered by the behaviors module, fixed once the host is built. Each kind's code is made once, the first time a
/// behavior that uses it is compiled, with the host's services.
/// </remarks>
public sealed class NodeCatalog
{
    /// <summary>The branch's id: the way on chosen by a condition.</summary>
    public const string Branch = "branch";

    /// <summary>The id of a variable set: <c>{"variable": "name", "value": ...}</c>.</summary>
    public const string SetVariable = "variable.set";

    /// <summary>The id of a variable read, its <see cref="GraphPins.Value"/> wired to another node: <c>{"variable": "name"}</c>.</summary>
    public const string GetVariable = "variable.get";

    private readonly Dictionary<string, NodeDefinition> _byId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _code = new(StringComparer.Ordinal);
    private readonly IServiceProvider _services;

    /// <summary>Gets every kind, D3B's own first, then the modules' in the order they registered them, then the activities'.</summary>
    public IReadOnlyList<NodeDefinition> All { get; }

    /// <summary>Initializes a new instance of the <see cref="NodeCatalog"/> class.</summary>
    /// <param name="declared">The kinds the modules declared.</param>
    /// <param name="activities">The activities, a node each.</param>
    /// <param name="services">The host's services, which make each kind's code.</param>
    /// <exception cref="InvalidOperationException">Two kinds share an id.</exception>
    public NodeCatalog(IEnumerable<NodeDefinition> declared, ActivityCatalog activities, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(services);

        _services = services;

        var branch = new NodeBuilder<BranchValues>();

        branch.Values.Flag(values => values.Condition, BehaviorTexts.Text("BranchCondition"));

        All =
        [
            new NodeDefinition(Branch, NodeRole.Branch, BehaviorTexts.Text("Branch"), BehaviorTexts.Text("BranchHelp"), null, branch.Values.Build(), false, null),
            new NodeDefinition(SetVariable, NodeRole.SetVariable, BehaviorTexts.Text("SetVariable"), BehaviorTexts.Text("SetVariableHelp"), null, ActivitySettings.None, true, null),
            new NodeDefinition(GetVariable, NodeRole.GetVariable, BehaviorTexts.Text("GetVariable"), BehaviorTexts.Text("GetVariableHelp"), null, ActivitySettings.None, false, null),
            .. declared,
            .. activities.All.Select(activity => new NodeDefinition(activity.Id, NodeRole.Activity, activity.Name, BehaviorTexts.Text("ActivityHelp"), null, activity.Settings, false, null, activity)),
        ];

        foreach (var definition in All)
        {
            if (!_byId.TryAdd(definition.Id, definition))
                throw new InvalidOperationException($"Two nodes have the id {definition.Id}.");
        }
    }

    /// <summary>Finds a kind by its id.</summary>
    /// <param name="id">The kind's id.</param>
    /// <param name="definition">The kind, when one has the id.</param>
    /// <returns>Whether a kind has the id.</returns>
    public bool TryGet(string id, [NotNullWhen(true)] out NodeDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _byId.TryGetValue(id, out definition);
    }

    internal object CodeOf(NodeDefinition definition)
    {
        return _code.GetOrAdd(definition.Id, static (_, state) => state.Definition.Create(state.Services), (Definition: definition, Services: _services));
    }
}
