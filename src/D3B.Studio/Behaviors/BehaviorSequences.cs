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

    /// <summary>The id of the node that plays a list of the project as a step of another (D420).</summary>
    public const string Play = "sequence.play";

    /// <summary>Gets the nodes the list editor offers as steps, in the order of its menu: those that need no map to be set up.</summary>
    public static ImmutableArray<string> Offered { get; } =
    [
        RouteActions.Follow,
        Play,
        RouteActions.Harvest,
        RouteActions.Fight,
        RouteActions.HavenBag,
        RouteActions.Wait,
        "note",
        "pause",
        "stop",
    ];

    /// <summary>Gets the conditions a repeat of a list or of a step can wait on ("until"), in the order of its menu: those that read the character and need no variable (D420).</summary>
    public static ImmutableArray<string> OfferedConditions { get; } =
    [
        "pods.atLeast",
        "life.atLeast",
        "level.atLeast",
        "job.levelAtLeast",
        "kamas.atLeast",
        "items.atLeast",
        "subArea.in",
        "area.in",
        "fight.last",
        "run.longerThan",
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

        return StepsOf(state.Actions) is { } steps ? new BehaviorSequence(steps, state.Repeat) : null;
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

            nodes.Add(new GraphNode(IdOf(index), step.Node, step.Values) { Repeat = step.Repeat, Position = new NodePosition(Margin + (index * Spacing), Margin) });

            if (index > 0)
                links.Add(new GraphLink(IdOf(index - 1), GraphPins.Next, IdOf(index)));
        }

        var state = new BehaviorState(StateId, behavior.Name)
        {
            Repeat = sequence.Repeat,
            Actions = new ActionGraph(sequence.Steps.IsEmpty ? null : IdOf(0), nodes.ToImmutable(), links.ToImmutable()),
        };

        return behavior with { Always = [], Rules = [], Variables = [], Parameters = [], States = [state] };
    }

    /// <summary>Lists the routes a behavior follows, by their ids.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <returns>The routes' ids, each once, in the order they are first followed.</returns>
    public static IReadOnlyList<string> RoutesOf(BehaviorAsset behavior)
    {
        return Referenced(behavior, RouteActions.Follow, nameof(RouteFollowValues.Route));
    }

    /// <summary>Lists the lists a behavior plays as steps, by their behaviors' ids (D420).</summary>
    /// <param name="behavior">The behavior.</param>
    /// <returns>The lists' ids, each once, in the order they are first played.</returns>
    public static IReadOnlyList<string> SequencesOf(BehaviorAsset behavior)
    {
        return Referenced(behavior, Play, nameof(SequenceValues.Sequence));
    }

    /// <summary>Tells whether a behavior plays a list, directly or through the lists it plays: such a list would play itself.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="target">The list's behavior id.</param>
    /// <param name="find">Finds a behavior by its id, or <see langword="null"/> when the project has none.</param>
    /// <returns>Whether <paramref name="target"/> is among the lists it plays, or those they play.</returns>
    public static bool Reaches(BehaviorAsset behavior, string target, Func<string, BehaviorAsset?> find)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(find);

        var seen = new HashSet<string>(StringComparer.Ordinal) { behavior.Id };
        var pending = new Stack<BehaviorAsset>([behavior]);

        while (pending.TryPop(out var current))
        {
            foreach (var id in SequencesOf(current))
            {
                if (id == target)
                    return true;

                if (seen.Add(id) && find(id) is { } next)
                    pending.Push(next);
            }
        }

        return false;
    }

    private static List<string> Referenced(BehaviorAsset behavior, string node, string key)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var ids = new List<string>();

        foreach (var step in Nodes(behavior.Always).Concat(Nodes(behavior.States)))
        {
            if (step.Node == node && step.Values.TryGetValue(key, out var value) && value is JsonValue text && text.TryGetValue<string>(out var id) && id.Length > 0 && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
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

            steps.Add(new NodeCall(node.Node, node.Values) { Repeat = node.Repeat });
            at = next.GetValueOrDefault(at);
        }

        return byId.Count == 0 ? steps.ToImmutable() : null;
    }
}
