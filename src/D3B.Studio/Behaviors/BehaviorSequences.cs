using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.Studio.Behaviors;

/// <summary>Reads a behavior as a list of steps and writes a list as a behavior (D386).</summary>
/// <remarks>
/// A behavior is a list when it is one state, without conditions, whose actions follow one another by their way on and nothing
/// else: no layer Always, rule, variable, public setting, branch or wire of data. Anything more is the canvas's (7e), and the list
/// editor leaves such a behavior as it is.
/// </remarks>
public static class BehaviorSequences
{
    private const string StateId = "main";
    private const string StepPrefix = "step";
    private const double Spacing = 260;
    private const double Margin = 40;

    /// <summary>Gets the nodes the list editor offers as steps, in the order of its menu: those that need no map to be set up.</summary>
    public static ImmutableArray<string> Offered { get; } =
    [
        RouteActions.Follow,
        RouteActions.Harvest,
        RouteActions.Fight,
        RouteActions.HavenBag,
        RouteActions.Wait,
        "note",
        "pause",
        "stop",
    ];

    /// <summary>Reads a behavior as a list of steps.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <returns>The list, or <see langword="null"/> when the behavior is more than one.</returns>
    public static BehaviorSequence? Read(BehaviorAsset behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        if (!behavior.Always.IsEmpty || !behavior.Rules.IsEmpty || !behavior.Variables.IsEmpty || !behavior.Parameters.IsEmpty || behavior.States.Length > 1)
            return null;

        if (behavior.States.IsEmpty)
            return BehaviorSequence.Empty;

        var state = behavior.States[0];

        if (state.When is not null || state.IsUrgent || !state.States.IsEmpty)
            return null;

        return StepsOf(state.Actions) is { } steps ? new BehaviorSequence(steps, state.Repeats) : null;
    }

    /// <summary>Writes a list of steps into a behavior, which keeps its identity, name and file.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="sequence">The list.</param>
    /// <returns>The behavior, now one state whose actions are the steps.</returns>
    public static BehaviorAsset Write(BehaviorAsset behavior, BehaviorSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(sequence);

        var nodes = ImmutableArray.CreateBuilder<GraphNode>(sequence.Steps.Length);
        var links = ImmutableArray.CreateBuilder<GraphLink>(Math.Max(0, sequence.Steps.Length - 1));

        for (var index = 0; index < sequence.Steps.Length; index++)
        {
            var step = sequence.Steps[index];

            nodes.Add(new GraphNode(IdOf(index), step.Node, step.Values) { Position = new NodePosition(Margin + (index * Spacing), Margin) });

            if (index > 0)
                links.Add(new GraphLink(IdOf(index - 1), GraphPins.Next, IdOf(index)));
        }

        var state = new BehaviorState(StateId, behavior.Name)
        {
            Repeats = sequence.Repeats,
            Actions = new ActionGraph(sequence.Steps.IsEmpty ? null : IdOf(0), nodes.ToImmutable(), links.ToImmutable()),
        };

        return behavior with { Always = [], Rules = [], Variables = [], Parameters = [], States = [state] };
    }

    /// <summary>Lists the routes a behavior follows, by their ids.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <returns>The routes' ids, each once, in the order they are first followed.</returns>
    public static IReadOnlyList<string> RoutesOf(BehaviorAsset behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var routes = new List<string>();

        foreach (var node in Nodes(behavior.Always).Concat(Nodes(behavior.States)))
        {
            if (node.Node == RouteActions.Follow && node.Values.TryGetValue(nameof(RouteFollowValues.Route), out var value) && value is JsonValue text && text.TryGetValue<string>(out var route) && route.Length > 0 && !routes.Contains(route))
                routes.Add(route);
        }

        return routes;
    }

    private static IEnumerable<GraphNode> Nodes(ImmutableArray<BehaviorState> states)
    {
        return states.SelectMany(Nodes);
    }

    private static IEnumerable<GraphNode> Nodes(BehaviorState state)
    {
        return state.Actions.Nodes.Concat(Nodes(state.States));
    }

    private static string IdOf(int index)
    {
        return StepPrefix + (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static ImmutableArray<NodeCall>? StepsOf(ActionGraph graph)
    {
        if (graph.Nodes.IsEmpty)
            return graph.Entry is null && graph.Links.IsEmpty ? [] : null;

        var byId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);

        foreach (var node in graph.Nodes)
        {
            if (node.IsDisabled || !byId.TryAdd(node.Id, node))
                return null;
        }

        var next = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var link in graph.Links)
        {
            if (link.IsData || link.Pin != GraphPins.Next || !byId.ContainsKey(link.From) || !byId.ContainsKey(link.To) || !next.TryAdd(link.From, link.To))
                return null;
        }

        var steps = ImmutableArray.CreateBuilder<NodeCall>(graph.Nodes.Length);
        var at = graph.Entry;

        while (at is not null)
        {
            // A node reached twice is a cycle, which a list is not.
            if (!byId.Remove(at, out var node))
                return null;

            steps.Add(new NodeCall(node.Node, node.Values));
            at = next.GetValueOrDefault(at);
        }

        return byId.Count == 0 ? steps.ToImmutable() : null;
    }
}
