using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Bot.Assets;
using D3B.Bot.Settings;

namespace D3B.Bot.Behaviors;

/// <summary>Compiles a behavior with a profile's values once, before it runs: every node found, every value read and every wire typed (D297, D300).</summary>
/// <remarks>
/// <para>
/// A value is chosen, bound to a public setting, whose value the profile may change, or to a variable, read as the node
/// plays; a wire from a condition or a variable's read brings it instead (D298, D311). Values are read as an activity's
/// settings are (D98), then checked: a behavior that does not hold does not run, and says where and why.
/// </para>
/// <para>
/// A value the profile changes that no longer reads stops the profile, as a setting reset did (D99): it is looked at
/// before the bot plays a value it was not given. One for a public setting the behavior no longer declares is passed
/// over, as a key no longer declared was (D98).
/// </para>
/// </remarks>
internal sealed class BehaviorCompiler
{
    private const string ParameterKey = "$param";
    private const string VariableKey = "$var";
    private const string VariableName = "variable";
    private const string VariableValue = "value";

    private readonly BehaviorAsset _behavior;
    private readonly IReadOnlyDictionary<string, JsonNode?> _profile;
    private readonly NodeCatalog _nodes;
    private readonly GameReferences? _game;
    private readonly List<BehaviorProblem> _problems = [];
    private readonly Dictionary<string, BehaviorParameter> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BehaviorVariable> _variables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private bool _urgent;

    private BehaviorCompiler(BehaviorAsset behavior, IReadOnlyDictionary<string, JsonNode?> profile, NodeCatalog nodes, GameReferences? game)
    {
        _behavior = behavior;
        _profile = profile;
        _nodes = nodes;
        _game = game;
    }

    /// <summary>Compiles a behavior with a profile's values.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="profile">The values the profile changes, by public setting; none to play the behavior as it is.</param>
    /// <param name="nodes">The kinds of nodes.</param>
    /// <param name="game">The game's values, to check those the values name, or <see langword="null"/> to leave them unchecked.</param>
    /// <returns>The behavior ready to run, or what is wrong with it.</returns>
    public static BehaviorCompilation Compile(BehaviorAsset behavior, IReadOnlyDictionary<string, JsonNode?> profile, NodeCatalog nodes, GameReferences? game)
    {
        var compiler = new BehaviorCompiler(behavior, profile, nodes, game);
        var compiled = compiler.Run();

        return new BehaviorCompilation(compiler._problems.Count == 0 ? compiled : null, compiler._problems);
    }

    private static bool Accepts(SettingDefinition input, VariableType type)
    {
        return !input.IsList && (type, input.Kind) is (VariableType.Flag, SettingKind.Flag) or (VariableType.Number, SettingKind.Number) or (VariableType.Text, SettingKind.Text);
    }

    private static string? Bound(JsonNode? node, string key)
    {
        return node is JsonObject { Count: 1 } bound && bound[key] is JsonValue name && name.TryGetValue<string>(out var text) ? text : null;
    }

    private static string In(string where, string id)
    {
        return where.Length == 0 ? id : $"{where}/{id}";
    }

    private CompiledBehavior Run()
    {
        foreach (var parameter in _behavior.Parameters)
        {
            if (!_parameters.TryAdd(parameter.Key, parameter))
                Problem(BehaviorProblemKind.IdRepeated, parameter.Key, "Two public settings have this key.");
        }

        foreach (var variable in _behavior.Variables)
        {
            if (!_variables.TryAdd(variable.Name, variable))
                Problem(BehaviorProblemKind.IdRepeated, variable.Name, "Two variables have this name.");
            else if (!VariableValues.TryRead(variable.Type, variable.Value, out _))
                Problem(BehaviorProblemKind.ValueDoesNotRead, variable.Name, $"Its value is no {variable.Type}.");
        }

        var always = States(_behavior.Always, "");
        var states = States(_behavior.States, "");
        var rules = _behavior.Rules.Select(Rule).OfType<CompiledRule>().ToImmutableArray();

        return new CompiledBehavior
        {
            Id = _behavior.Id,
            Always = always,
            States = states,
            Rules = rules,
            Variables = [.. _variables.Values],
            HasUrgent = _urgent,
        };
    }

    private void Problem(BehaviorProblemKind kind, string where, string detail)
    {
        _problems.Add(new BehaviorProblem(kind, where, detail));
    }

    private bool Unique(string id, string where)
    {
        if (_ids.Add(id))
            return true;

        Problem(BehaviorProblemKind.IdRepeated, where, $"Another state, rule or node has the id {id}.");

        return false;
    }

    private ImmutableArray<CompiledState> States(ImmutableArray<BehaviorState> states, string where)
    {
        var compiled = ImmutableArray.CreateBuilder<CompiledState>(states.Length);

        foreach (var state in states)
        {
            var at = In(where, state.Id);

            Unique(state.Id, at);

            if (!state.States.IsEmpty && !state.Actions.Nodes.IsEmpty)
                Problem(BehaviorProblemKind.StateMixed, at, "A state holds states or actions, not both.");

            _urgent |= state.IsUrgent;
            compiled.Add(new CompiledState
            {
                Id = state.Id,
                Name = state.Name,
                When = state.When is { } when ? Condition(when, at) : null,
                IsUrgent = state.IsUrgent,
                Repeats = state.Repeats,
                States = States(state.States, at),
                Entry = Graph(state.Actions, at, rule: false),
            });
        }

        return compiled.MoveToImmutable();
    }

    private CompiledRule? Rule(BehaviorRule rule)
    {
        Unique(rule.Id, rule.Id);

        if (Definition(rule.On.Node, rule.Id, NodeRole.Event) is not { } definition)
            return null;

        return new CompiledRule
        {
            Id = rule.Id,
            Name = rule.Name.Length > 0 ? rule.Name : rule.Id,
            Event = (IEventNode)_nodes.CodeOf(definition),
            EventValues = Bind(definition, rule.On.Values, [], rule.Id),
            When = rule.When is { } when ? Condition(when, rule.Id) : null,
            Entry = Graph(rule.Actions, rule.Id, rule: true),
        };
    }

    private NodeDefinition? Definition(string kind, string where, NodeRole role)
    {
        if (!_nodes.TryGet(kind, out var definition))
        {
            Problem(BehaviorProblemKind.NodeUnknown, where, $"No module declares the node {kind}.");

            return null;
        }

        if (definition.Role != role)
        {
            Problem(BehaviorProblemKind.NodeMisplaced, where, $"The node {kind} is a {definition.Role}, where a {role} goes.");

            return null;
        }

        return definition;
    }

    private CompiledCondition Condition(Condition condition, string where)
    {
        switch (condition)
        {
            case AllCondition all:
                return CompiledCondition.All([.. all.Conditions.Select(item => Condition(item, where))]);
            case AnyCondition any:
                return CompiledCondition.Any([.. any.Conditions.Select(item => Condition(item, where))]);
            case NotCondition not:
                return CompiledCondition.Not(Condition(not.Condition, where));
            case NodeCondition { Call: var call } when Definition(call.Node, where, NodeRole.Condition) is { } definition:
                return CompiledCondition.Node((IConditionNode)_nodes.CodeOf(definition), Bind(definition, call.Values, [], where));
            default:
                // Said already: the behavior does not run, so what stands in for the condition is never read.
                return CompiledCondition.All([]);
        }
    }

    private BoundValues Bind(NodeDefinition definition, IReadOnlyDictionary<string, JsonNode?> values, IReadOnlyList<(string Input, Func<Evaluation, object?> Source, VariableType Type)> wired, string where)
    {
        var settings = definition.Values;
        var bound = settings.CreateDefault();
        var brought = new List<(SettingDefinition Setting, Func<Evaluation, object?> Source)>();

        foreach (var (input, source, type) in wired)
        {
            if (settings.All.FirstOrDefault(setting => setting.Key == input) is not { } setting)
                Problem(BehaviorProblemKind.LinkBroken, where, $"A wire brings {input}, which the node {definition.Id} does not have.");
            else if (!Accepts(setting, type))
                Problem(BehaviorProblemKind.TypesDiffer, where, $"A wire brings a {type} to {input}, a {setting.Kind}{(setting.IsList ? " list" : "")}.");
            else
                brought.Add((setting, source));
        }

        foreach (var (key, json) in values)
        {
            if (settings.All.FirstOrDefault(setting => setting.Key == key) is not { } setting)
            {
                Problem(BehaviorProblemKind.ValueUnknown, where, $"The node {definition.Id} has no value {key}.");

                continue;
            }

            // A wire wins over the value chosen, which stays for when it is unwired.
            if (bound is not null && !brought.Exists(item => item.Setting == setting))
                BindValue(setting, json, bound, brought, where);
        }

        if (bound is not null)
        {
            foreach (var problem in settings.Check(bound, _game))
            {
                if (problem.Setting is { } setting && brought.Exists(item => item.Setting == setting))
                    continue;

                var detail = problem.Setting is { } named ? $"{named.Key}: {problem.Kind}" : $"{problem.Message?.In(CultureInfo.InvariantCulture)}";

                Problem(BehaviorProblemKind.ValueInvalid, where, detail);
            }
        }

        return new BoundValues(settings, bound, [.. brought]);
    }

    private void BindValue(SettingDefinition setting, JsonNode? json, object values, List<(SettingDefinition Setting, Func<Evaluation, object?> Source)> brought, string where)
    {
        if (Bound(json, ParameterKey) is { } key)
        {
            if (!_parameters.TryGetValue(key, out var parameter))
                Problem(BehaviorProblemKind.ParameterUnknown, where, $"{setting.Key} is bound to the public setting {key}, which the behavior does not declare.");
            else if (_profile.TryGetValue(key, out var changed) && !setting.TryRead(values, changed))
                Problem(BehaviorProblemKind.ProfileValueDoesNotRead, key, $"The profile's value of {key} does not read as {setting.Key}'s.");
            else if (!_profile.ContainsKey(key) && !setting.TryRead(values, parameter.Value))
                Problem(BehaviorProblemKind.ValueDoesNotRead, key, $"The public setting's value does not read as {setting.Key}'s.");

            return;
        }

        if (Bound(json, VariableKey) is { } name)
        {
            if (!_variables.TryGetValue(name, out var variable))
                Problem(BehaviorProblemKind.VariableUnknown, where, $"{setting.Key} is bound to the variable {name}, which the behavior does not declare.");
            else if (!Accepts(setting, variable.Type))
                Problem(BehaviorProblemKind.TypesDiffer, where, $"The variable {name} is a {variable.Type}, {setting.Key} a {setting.Kind}{(setting.IsList ? " list" : "")}.");
            else
                brought.Add((setting, evaluation => evaluation.Variables.Get(name)));

            return;
        }

        if (!setting.TryRead(values, json))
            Problem(BehaviorProblemKind.ValueDoesNotRead, where, $"{setting.Key} does not read as a {setting.Kind}{(setting.IsList ? " list" : "")}.");
    }

    private CompiledStep? Graph(ActionGraph graph, string where, bool rule)
    {
        if (graph.Nodes.IsEmpty)
            return null;

        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var definitions = new Dictionary<string, NodeDefinition>(StringComparer.Ordinal);

        foreach (var node in graph.Nodes)
        {
            var at = In(where, node.Id);

            if (!Unique(node.Id, at) || !nodes.TryAdd(node.Id, node))
                continue;

            if (_nodes.TryGet(node.Node, out var definition))
                definitions[node.Id] = definition;
            else
                Problem(BehaviorProblemKind.NodeUnknown, at, $"No module declares the node {node.Node}.");
        }

        var graphCompiler = new GraphCompiler(this, graph, nodes, definitions, where);

        return graphCompiler.Compile(rule);
    }

    /// <summary>One canvas: its values brought by wires, its way of execution, checked for loops.</summary>
    private sealed class GraphCompiler
    {
        private readonly BehaviorCompiler _behavior;
        private readonly ActionGraph _graph;
        private readonly Dictionary<string, GraphNode> _nodes;
        private readonly Dictionary<string, NodeDefinition> _definitions;
        private readonly string _where;
        private readonly Dictionary<string, (Func<Evaluation, object?> Get, VariableType Type)?> _sources = new(StringComparer.Ordinal);
        private readonly HashSet<string> _reading = new(StringComparer.Ordinal);

        public GraphCompiler(BehaviorCompiler behavior, ActionGraph graph, Dictionary<string, GraphNode> nodes, Dictionary<string, NodeDefinition> definitions, string where)
        {
            _behavior = behavior;
            _graph = graph;
            _nodes = nodes;
            _definitions = definitions;
            _where = where;
        }

        private static CompiledStep? Skip(CompiledStep? step, HashSet<CompiledStep> disabled)
        {
            var passed = 0;

            while (step is not null && disabled.Contains(step))
            {
                // Disabled nodes wired in a circle: the loop is said once the way is checked.
                if (++passed > disabled.Count)
                    return null;

                step = step.Next;
            }

            return step;
        }

        public CompiledStep? Compile(bool rule)
        {
            var steps = new Dictionary<string, CompiledStep>(StringComparer.Ordinal);
            var disabled = new HashSet<CompiledStep>();

            foreach (var (id, definition) in _definitions)
            {
                var node = _nodes[id];
                var at = In(_where, id);
                CompiledStep? step = definition.Role switch
                {
                    NodeRole.Action => new CompiledStep.Action { Id = id, Node = (IActionNode)_behavior._nodes.CodeOf(definition), Sentence = definition.Sentence, Values = _behavior.Bind(definition, node.Values, Wires(id), at) },
                    NodeRole.Activity => new CompiledStep.Activity { Id = id, Definition = definition.Activity!, Settings = _behavior.Bind(definition, node.Values, Wires(id), at) },
                    NodeRole.Branch => Branch(definition, node, at),
                    NodeRole.SetVariable => SetVariable(node, at),
                    _ => null,
                };

                if (definition.Role is NodeRole.Event)
                    _behavior.Problem(BehaviorProblemKind.NodeMisplaced, at, $"The event {definition.Id} starts a rule; it is no action.");

                if (step is null)
                    continue;

                steps[id] = step;

                if (node.IsDisabled && step is CompiledStep.Branch)
                    _behavior.Problem(BehaviorProblemKind.NodeMisplaced, at, "A branch is not disabled: delete it, or wire around it.");
                else if (node.IsDisabled)
                    disabled.Add(step);
            }

            foreach (var link in _graph.Links)
            {
                if (link.IsData)
                {
                    CheckData(link);

                    continue;
                }

                Wire(link, steps);
            }

            CompiledStep? entry = null;

            if (_graph.Entry is null && steps.Count > 0)
                _behavior.Problem(BehaviorProblemKind.LinkBroken, _where, "The actions have no first node.");
            else if (_graph.Entry is { } first && !steps.TryGetValue(first, out entry))
                _behavior.Problem(BehaviorProblemKind.LinkBroken, _where, $"The first node, {first}, is no action, branch nor variable set.");

            entry = Skip(entry, disabled);

            foreach (var step in steps.Values)
            {
                step.Next = Skip(step.Next, disabled);
                step.Otherwise = Skip(step.Otherwise, disabled);
            }

            CheckLoops(entry);

            if (rule)
                CheckInstant(entry);

            return entry;
        }

        private CompiledStep.Branch Branch(NodeDefinition definition, GraphNode node, string at)
        {
            var values = _behavior.Bind(definition, node.Values, Wires(node.Id), at);

            return new CompiledStep.Branch { Id = node.Id, Condition = evaluation => ((BranchValues)values.Resolve(evaluation)!).Condition };
        }

        private CompiledStep.SetVariable? SetVariable(GraphNode node, string at)
        {
            if (Variable(node, at) is not { } variable)
                return null;

            Func<Evaluation, object>? value = null;

            if (Wires(node.Id) is [var (input, source, type), ..])
            {
                if (input != VariableValue)
                    _behavior.Problem(BehaviorProblemKind.LinkBroken, at, $"A wire brings {input} to a variable set, which takes its {VariableValue}.");
                else if (type != variable.Type)
                    _behavior.Problem(BehaviorProblemKind.TypesDiffer, at, $"A wire brings a {type} to the variable {variable.Name}, a {variable.Type}.");
                else
                    value = evaluation => source(evaluation)!;
            }
            else if (!node.Values.TryGetValue(VariableValue, out var json))
            {
                var initial = VariableValues.Default(variable.Type);

                value = _ => initial;
            }
            else if (Bound(json, VariableKey) is { } name)
            {
                if (!_behavior._variables.TryGetValue(name, out var other))
                    _behavior.Problem(BehaviorProblemKind.VariableUnknown, at, $"The variable {name} is not declared.");
                else if (other.Type != variable.Type)
                    _behavior.Problem(BehaviorProblemKind.TypesDiffer, at, $"The variable {name} is a {other.Type}, {variable.Name} a {variable.Type}.");
                else
                    value = evaluation => evaluation.Variables.Get(name);
            }
            else
            {
                var read = json;
                var known = true;

                if (Bound(json, ParameterKey) is { } key)
                {
                    known = _behavior._parameters.TryGetValue(key, out var parameter);
                    read = known ? _behavior._profile.GetValueOrDefault(key, parameter!.Value) : null;

                    if (!known)
                        _behavior.Problem(BehaviorProblemKind.ParameterUnknown, at, $"The value is bound to the public setting {key}, which the behavior does not declare.");
                }

                if (known && read is not null && VariableValues.TryRead(variable.Type, read, out var fixedValue))
                    value = _ => fixedValue;
                else if (known)
                    _behavior.Problem(BehaviorProblemKind.ValueDoesNotRead, at, $"The value is no {variable.Type}, as the variable {variable.Name}.");
            }

            foreach (var key in node.Values.Keys.Where(key => key is not (VariableName or VariableValue)))
                _behavior.Problem(BehaviorProblemKind.ValueUnknown, at, $"A variable set has no value {key}.");

            return value is null ? null : new CompiledStep.SetVariable { Id = node.Id, Variable = variable.Name, Value = value };
        }

        private BehaviorVariable? Variable(GraphNode node, string at)
        {
            var name = node.Values.TryGetValue(VariableName, out var json) && json is JsonValue text && text.TryGetValue<string>(out var named) ? named : null;

            if (name is not null && _behavior._variables.TryGetValue(name, out var variable))
                return variable;

            _behavior.Problem(BehaviorProblemKind.VariableUnknown, at, name is null ? "The node names no variable." : $"The variable {name} is not declared.");

            return null;
        }

        private List<(string Input, Func<Evaluation, object?> Source, VariableType Type)> Wires(string id)
        {
            var wires = new List<(string, Func<Evaluation, object?>, VariableType)>();

            foreach (var link in _graph.Links.Where(link => link.IsData && link.To == id))
            {
                if (Source(link.From) is { } source)
                    wires.Add((link.Input!, source.Get, source.Type));
            }

            return wires;
        }

        private (Func<Evaluation, object?> Get, VariableType Type)? Source(string id)
        {
            if (_sources.TryGetValue(id, out var known))
                return known;

            if (!_nodes.TryGetValue(id, out var node) || !_definitions.TryGetValue(id, out var definition))
                return null;

            var at = In(_where, id);

            if (!_reading.Add(id))
            {
                _behavior.Problem(BehaviorProblemKind.Loop, at, "Values are wired in a circle.");

                return null;
            }

            (Func<Evaluation, object?> Get, VariableType Type)? source = null;

            if (definition.Role is NodeRole.Condition)
            {
                var condition = CompiledCondition.Node((IConditionNode)_behavior._nodes.CodeOf(definition), _behavior.Bind(definition, node.Values, Wires(id), at));

                source = (evaluation => condition.Holds(evaluation), VariableType.Flag);
            }
            else if (definition.Role is NodeRole.GetVariable && Variable(node, at) is { } variable)
            {
                source = (evaluation => evaluation.Variables.Get(variable.Name), variable.Type);
            }

            _reading.Remove(id);
            _sources[id] = source;

            return source;
        }

        private void CheckData(GraphLink link)
        {
            var at = In(_where, link.From);

            if (!_nodes.ContainsKey(link.From) || !_nodes.ContainsKey(link.To))
                _behavior.Problem(BehaviorProblemKind.LinkBroken, at, $"A wire joins {link.From} and {link.To}, one of which is not there.");
            else if (link.Pin != GraphPins.Value || _definitions.GetValueOrDefault(link.From)?.Role is not (NodeRole.Condition or NodeRole.GetVariable))
                _behavior.Problem(BehaviorProblemKind.LinkBroken, at, "A value leaves a condition or a variable's read, by its value.");
            else if (_graph.Links.Count(other => other.IsData && other.To == link.To && other.Input == link.Input) > 1)
                _behavior.Problem(BehaviorProblemKind.LinkBroken, In(_where, link.To), $"Two wires bring {link.Input}.");
        }

        private void Wire(GraphLink link, Dictionary<string, CompiledStep> steps)
        {
            var at = In(_where, link.From);

            if (!steps.TryGetValue(link.From, out var from) || !steps.TryGetValue(link.To, out var to))
            {
                _behavior.Problem(BehaviorProblemKind.LinkBroken, at, $"A wire of execution joins {link.From} and {link.To}, which are not both actions, branches or variable sets.");

                return;
            }

            var otherwise = from is CompiledStep.Branch && link.Pin == GraphPins.False;
            var valid = from is CompiledStep.Branch ? link.Pin is GraphPins.True or GraphPins.False : link.Pin == GraphPins.Next;

            if (!valid)
            {
                _behavior.Problem(BehaviorProblemKind.LinkBroken, at, $"The node has no way out {link.Pin}.");

                return;
            }

            if ((otherwise ? from.Otherwise : from.Next) is not null)
            {
                _behavior.Problem(BehaviorProblemKind.LinkBroken, at, $"Two wires leave its way out {link.Pin}.");

                return;
            }

            if (otherwise)
                from.Otherwise = to;
            else
                from.Next = to;
        }

        private void CheckLoops(CompiledStep? entry)
        {
            var done = new HashSet<CompiledStep>();
            var path = new HashSet<CompiledStep>();

            // Iterative, so that a long canvas never runs the stack out.
            var stack = new Stack<(CompiledStep Step, IEnumerator<CompiledStep> Ways)>();

            if (entry is null)
                return;

            stack.Push((entry, entry.Ways().GetEnumerator()));
            path.Add(entry);

            while (stack.Count > 0)
            {
                var (step, ways) = stack.Peek();

                if (!ways.MoveNext())
                {
                    ways.Dispose();
                    stack.Pop();
                    path.Remove(step);
                    done.Add(step);

                    continue;
                }

                var next = ways.Current;

                if (path.Contains(next))
                {
                    _behavior.Problem(BehaviorProblemKind.Loop, In(_where, next.Id), "The way of execution comes back here: a state's actions start again only when it repeats.");

                    continue;
                }

                if (done.Contains(next))
                    continue;

                path.Add(next);
                stack.Push((next, next.Ways().GetEnumerator()));
            }
        }

        private void CheckInstant(CompiledStep? entry)
        {
            var seen = new HashSet<CompiledStep>();
            var waiting = new Queue<CompiledStep>();

            if (entry is not null)
                waiting.Enqueue(entry);

            while (waiting.TryDequeue(out var step))
            {
                if (!seen.Add(step))
                    continue;

                if (step is CompiledStep.Activity or CompiledStep.Action { IsInstant: false })
                    _behavior.Problem(BehaviorProblemKind.NotInstant, In(_where, step.Id), "A rule plays only actions played at once; this one waits.");

                foreach (var next in step.Ways())
                    waiting.Enqueue(next);
            }
        }
    }
}
