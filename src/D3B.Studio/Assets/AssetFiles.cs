using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Assets;

/// <summary>An asset's file: its extension by kind, and its JSON, read and written (D296).</summary>
/// <remarks>
/// <para>
/// A file holds its asset's id, kind and format's version, and the client build its maps' ids are of when it names maps;
/// its name is the file's, the extension said by its kind, such as <c>Harvest.behavior.json</c>. Values left as their
/// defaults are not written, so that a file says what was chosen.
/// </para>
/// <para>
/// A format newer than this version of D3B reads does not read, rather than lose what it does not know of; an older one
/// reads as it is, each version's changes read as the later sessions make them.
/// </para>
/// </remarks>
public static class AssetFiles
{
    /// <summary>The format's version this version of D3B writes.</summary>
    public const int Format = 1;

    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly (AssetKind Kind, string Name, string Extension)[] _kinds =
    [
        (AssetKind.Behavior, "behavior", ".behavior.json"),
        (AssetKind.Profile, "profile", ".profile.json"),
        (AssetKind.Route, "route", ".route.json"),
        (AssetKind.Zone, "zone", ".zone.json"),
        (AssetKind.List, "list", ".list.json"),
        (AssetKind.Planning, "planning", ".planning.json"),
        (AssetKind.FightPreferences, "fightPreferences", ".fight.json"),
    ];

    private static readonly string[] _header = ["id", "kind", "format", "client"];

    /// <summary>Gets the extension of an asset's file by its kind, such as <c>.behavior.json</c>.</summary>
    /// <param name="kind">The asset's kind.</param>
    /// <returns>The extension.</returns>
    public static string ExtensionOf(AssetKind kind)
    {
        return Array.Find(_kinds, entry => entry.Kind == kind).Extension;
    }

    /// <summary>Tells an asset's kind and name from its file's name.</summary>
    /// <param name="fileName">The file's name, with its extension.</param>
    /// <param name="kind">The kind its extension says.</param>
    /// <param name="name">The asset's name, the file's without the extension.</param>
    /// <returns>Whether the extension is an asset's.</returns>
    public static bool TryKindOf(string fileName, out AssetKind kind, out string name)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        foreach (var entry in _kinds)
        {
            if (fileName.Length > entry.Extension.Length && fileName.EndsWith(entry.Extension, StringComparison.OrdinalIgnoreCase))
            {
                kind = entry.Kind;
                name = fileName[..^entry.Extension.Length];

                return true;
            }
        }

        kind = default;
        name = "";

        return false;
    }

    /// <summary>Reads an asset's file.</summary>
    /// <param name="json">What the file holds.</param>
    /// <param name="name">The asset's name, the file's.</param>
    /// <param name="path">The file, relative to the project's folder.</param>
    /// <param name="kind">The kind its extension says, which the file must say too.</param>
    /// <returns>The asset.</returns>
    /// <exception cref="AssetFormatException">The file does not read as an asset of its kind.</exception>
    public static Asset Read(string json, string name, string path, AssetKind kind)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonObject root;

        try
        {
            root = JsonNode.Parse(json) as JsonObject ?? throw new AssetFormatException("The file holds no JSON object.") { Kind = AssetProblemKind.NotJson };
        }
        catch (JsonException exception)
        {
            throw new AssetFormatException($"The file is no JSON: {exception.Message}", exception) { Kind = AssetProblemKind.NotJson };
        }

        var id = Text(root, "id");
        var said = Text(root, "kind");

        if (!string.Equals(said, NameOf(kind), StringComparison.Ordinal))
            throw new AssetFormatException($"The file says it is a {said}, its extension a {NameOf(kind)}.") { Kind = AssetProblemKind.WrongKind };

        var format = root["format"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : throw new AssetFormatException("The file says no format.") { Kind = AssetProblemKind.Format };

        if (format > Format || format < 1)
            throw new AssetFormatException($"The file's format is {format}, which this version of D3B does not read: it reads up to {Format}.") { Kind = AssetProblemKind.Format };

        var client = root["client"]?.GetValue<string>();

        return kind switch
        {
            AssetKind.Behavior => ReadBehavior(root) with { Id = id, Name = name, Path = path, Client = client },
            AssetKind.Profile => new ProfileAsset { Id = id, Name = name, Path = path, Client = client, Behavior = Text(root, "behavior"), Values = Values(root["values"]) },
            AssetKind.Route => ReadRoute(root) with { Id = id, Name = name, Path = path, Client = client },
            AssetKind.Zone => ReadZone(root) with { Id = id, Name = name, Path = path, Client = client },
            _ => new PlannedAsset(kind) { Id = id, Name = name, Path = path, Client = client, Content = Rest(root) },
        };
    }

    /// <summary>Writes an asset's file.</summary>
    /// <param name="asset">The asset.</param>
    /// <returns>The JSON its file holds, indented.</returns>
    public static string Write(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var root = new JsonObject
        {
            ["id"] = asset.Id,
            ["kind"] = NameOf(asset.Kind),
            ["format"] = Format,
        };

        if (asset.Client is { } client)
            root["client"] = client;

        switch (asset)
        {
            case BehaviorAsset behavior:
                WriteBehavior(behavior, root);
                break;
            case ProfileAsset profile:
                root["behavior"] = profile.Behavior;
                root["values"] = Values(profile.Values);
                break;
            case RouteAsset route:
                WriteRoute(route, root);
                break;
            case ZoneAsset zone:
                WriteZone(zone, root);
                break;
            case PlannedAsset planned:
                foreach (var (key, node) in planned.Content)
                    root[key] = node?.DeepClone();

                break;
        }

        return root.ToJsonString(_indented) + "\n";
    }

    internal static ImmutableSortedDictionary<string, JsonNode?> Values(JsonNode? node)
    {
        if (node is null)
            return ImmutableSortedDictionary.Create<string, JsonNode?>(StringComparer.Ordinal);

        if (node is not JsonObject values)
            throw new AssetFormatException("Values are a JSON object.");

        var builder = ImmutableSortedDictionary.CreateBuilder<string, JsonNode?>(StringComparer.Ordinal);

        // A null is a value, such as a sub-area left unchosen.
        foreach (var (key, value) in values)
            builder[key] = value?.DeepClone();

        return builder.ToImmutable();
    }

    internal static JsonObject Values(ImmutableSortedDictionary<string, JsonNode?> values)
    {
        var json = new JsonObject();

        foreach (var (key, value) in values)
            json[key] = value?.DeepClone();

        return json;
    }

    private static string NameOf(AssetKind kind)
    {
        return Array.Find(_kinds, entry => entry.Kind == kind).Name;
    }

    private static string Text(JsonObject node, string key)
    {
        return node[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new AssetFormatException($"\"{key}\" is missing, or is no text.");
    }

    private static string? OptionalText(JsonObject node, string key)
    {
        return node[key] is null ? null : Text(node, key);
    }

    private static bool Flag(JsonObject node, string key)
    {
        return node[key] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
    }

    private static ImmutableArray<T> Items<T>(JsonObject node, string key, Func<JsonObject, T> read)
    {
        if (node[key] is null)
            return [];

        if (node[key] is not JsonArray items)
            throw new AssetFormatException($"\"{key}\" is a JSON array.");

        return [.. items.Select(item => read(item as JsonObject ?? throw new AssetFormatException($"The items of \"{key}\" are JSON objects.")))];
    }

    private static TEnum Choice<TEnum>(JsonObject node, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (node[key] is null)
            return fallback;

        return System.Enum.TryParse<TEnum>(Text(node, key), ignoreCase: true, out var value) && System.Enum.IsDefined(value)
            ? value
            : throw new AssetFormatException($"\"{key}\" is no {typeof(TEnum).Name}.");
    }

    private static string Camel<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static JsonObject Rest(JsonObject root)
    {
        var rest = new JsonObject();

        foreach (var (key, node) in root)
        {
            if (!_header.Contains(key))
                rest[key] = node?.DeepClone();
        }

        return rest;
    }

    private static RouteAsset ReadRoute(JsonObject root)
    {
        var details = Rest(root);

        details.Remove("anchors");
        details.Remove("mode");
        details.Remove("unreachable");

        return new RouteAsset
        {
            Id = "",
            Name = "",
            Anchors = Items(root, "anchors", ReadAnchor),
            Mode = Choice(root, "mode", RouteMode.Loop),
            Unreachable = Choice(root, "unreachable", RouteUnreachable.Skip),
            Details = details,
        };
    }

    private static RouteAnchor ReadAnchor(JsonObject node)
    {
        var map = node["map"] is JsonValue value && value.TryGetValue<long>(out var id) && id > 0 ? id : throw new AssetFormatException("An anchor has no map, or its id is no number.");
        var details = new JsonObject();

        foreach (var (key, detail) in node)
        {
            if (key is not ("map" or "pinned" or "actions"))
                details[key] = detail?.DeepClone();
        }

        return new RouteAnchor { MapId = map, IsPinned = Flag(node, "pinned"), Actions = Items(node, "actions", ReadCall), Details = details };
    }

    private static void WriteRoute(RouteAsset route, JsonObject root)
    {
        if (route.Mode != RouteMode.Loop)
            root["mode"] = Camel(route.Mode);

        if (route.Unreachable != RouteUnreachable.Skip)
            root["unreachable"] = Camel(route.Unreachable);

        foreach (var (key, node) in route.Details)
            root[key] = node?.DeepClone();

        root["anchors"] = new JsonArray([.. route.Anchors.Select(anchor =>
        {
            var json = new JsonObject { ["map"] = anchor.MapId };

            if (anchor.IsPinned)
                json["pinned"] = true;

            if (anchor.Actions.Length > 0)
                json["actions"] = new JsonArray([.. anchor.Actions.Select(call => (JsonNode)WriteCall(call))]);

            foreach (var (key, node) in anchor.Details)
                json[key] = node?.DeepClone();

            return (JsonNode)json;
        })]);
    }

    private static ZoneAsset ReadZone(JsonObject root)
    {
        var details = Rest(root);

        details.Remove("subAreas");
        details.Remove("maps");
        details.Remove("excluded");

        return new ZoneAsset
        {
            Id = "",
            Name = "",
            SubAreas = Numbers<int>(root, "subAreas"),
            Maps = Numbers<long>(root, "maps"),
            Excluded = Numbers<long>(root, "excluded"),
            Details = details,
        };
    }

    // Written ascending and without a repeat, so that a file reads the same however it was made.
    private static ImmutableArray<T> Numbers<T>(JsonObject node, string key)
        where T : struct, System.Numerics.INumber<T>
    {
        if (node[key] is null)
            return [];

        if (node[key] is not JsonArray items)
            throw new AssetFormatException($"\"{key}\" is a JSON array.");

        return [.. items.Select(item => item is JsonValue value && value.TryGetValue<T>(out var number) && number > T.Zero ? number : throw new AssetFormatException($"The items of \"{key}\" are ids.")).Distinct().Order()];
    }

    private static void WriteZone(ZoneAsset zone, JsonObject root)
    {
        foreach (var (key, node) in zone.Details)
            root[key] = node?.DeepClone();

        if (!zone.SubAreas.IsEmpty)
            root["subAreas"] = new JsonArray([.. zone.SubAreas.Distinct().Order().Select(id => (JsonNode)JsonValue.Create(id))]);

        if (!zone.Maps.IsEmpty)
            root["maps"] = new JsonArray([.. zone.Maps.Distinct().Order().Select(id => (JsonNode)JsonValue.Create(id))]);

        if (!zone.Excluded.IsEmpty)
            root["excluded"] = new JsonArray([.. zone.Excluded.Distinct().Order().Select(id => (JsonNode)JsonValue.Create(id))]);
    }

    private static BehaviorAsset ReadBehavior(JsonObject root)
    {
        return new BehaviorAsset
        {
            Id = "",
            Name = "",
            Parameters = Items(root, "parameters", node => new BehaviorParameter(Text(node, "key"), node.ContainsKey("value") ? node["value"]?.DeepClone() : throw new AssetFormatException("A public setting has no value."))),
            Variables = Items(root, "variables", node => new BehaviorVariable(Text(node, "name"), Choice(node, "type", VariableType.Flag), Choice(node, "scope", VariableScope.Run), node["value"]?.DeepClone())),
            Always = Items(root, "always", ReadState),
            Rules = Items(root, "rules", ReadRule),
            States = Items(root, "states", ReadState),
        };
    }

    private static BehaviorState ReadState(JsonObject node)
    {
        return new BehaviorState(Text(node, "id"), OptionalText(node, "name") ?? "")
        {
            When = node["when"] is { } when ? ReadCondition(when) : null,
            IsUrgent = Flag(node, "urgent"),
            Repeat = Flag(node, "repeats") ? StepRepeat.Forever : ReadRepeat(node["repeat"]),
            States = Items(node, "states", ReadState),
            Actions = node["actions"] is JsonObject actions ? ReadGraph(actions) : ActionGraph.Empty,
        };
    }

    private static BehaviorRule ReadRule(JsonObject node)
    {
        return new BehaviorRule(Text(node, "id"), OptionalText(node, "name") ?? "", ReadCall(node["on"] as JsonObject ?? throw new AssetFormatException("A rule has no event.")))
        {
            When = node["when"] is { } when ? ReadCondition(when) : null,
            Actions = node["actions"] is JsonObject actions ? ReadGraph(actions) : ActionGraph.Empty,
        };
    }

    private static StepRepeat ReadRepeat(JsonNode? node)
    {
        if (node is null)
            return StepRepeat.Once;

        if (node is not JsonObject repeat)
            throw new AssetFormatException("\"repeat\" is a JSON object.");

        if (repeat["times"] is { } times)
        {
            return times is JsonValue value && value.TryGetValue<int>(out var count) && count is >= 1 and <= StepRepeat.MostTimes
                ? StepRepeat.Times(count)
                : throw new AssetFormatException($"\"times\" is a whole number from 1 to {StepRepeat.MostTimes}.");
        }

        if (repeat["until"] is { } until)
            return StepRepeat.Until(ReadCall(until as JsonObject ?? throw new AssetFormatException("\"until\" is a condition's node.")));

        return Flag(repeat, "forever") ? StepRepeat.Forever : throw new AssetFormatException("A repeat is \"times\", \"until\" or \"forever\".");
    }

    private static JsonObject? WriteRepeat(StepRepeat repeat)
    {
        return repeat.Kind switch
        {
            RepeatKind.Times => new JsonObject { ["times"] = repeat.Count },
            RepeatKind.Until => new JsonObject { ["until"] = WriteCall(repeat.Condition!) },
            RepeatKind.Forever => new JsonObject { ["forever"] = true },
            _ => null,
        };
    }

    private static NodeCall ReadCall(JsonObject node)
    {
        return new NodeCall(Text(node, "node"), Values(node["values"]));
    }

    private static Condition ReadCondition(JsonNode node)
    {
        if (node is not JsonObject condition)
            throw new AssetFormatException("A condition is a JSON object.");

        if (condition["all"] is not null)
            return new AllCondition(Items(condition, "all", ReadCondition));

        if (condition["any"] is not null)
            return new AnyCondition(Items(condition, "any", ReadCondition));

        if (condition["not"] is { } not)
            return new NotCondition(ReadCondition(not));

        return new NodeCondition(ReadCall(condition));
    }

    private static ActionGraph ReadGraph(JsonObject node)
    {
        var layout = node["layout"] as JsonObject;

        return new ActionGraph(
            OptionalText(node, "entry"),
            Items(node, "nodes", item => new GraphNode(Text(item, "id"), Text(item, "node"), Values(item["values"]))
            {
                IsDisabled = Flag(item, "disabled"),
                Repeat = ReadRepeat(item["repeat"]),
                Position = PositionOf(layout, Text(item, "id")),
            }),
            Items(node, "links", item => new GraphLink(Text(item, "from"), Text(item, "pin"), Text(item, "to"), OptionalText(item, "input"))));
    }

    private static NodePosition PositionOf(JsonObject? layout, string id)
    {
        if (layout?[id] is not JsonArray position || position.Count != 2)
            return default;

        return new NodePosition(position[0]?.GetValue<double>() ?? 0, position[1]?.GetValue<double>() ?? 0);
    }

    private static void WriteBehavior(BehaviorAsset behavior, JsonObject root)
    {
        if (behavior.Parameters.Length > 0)
            root["parameters"] = new JsonArray([.. behavior.Parameters.Select(parameter => (JsonNode)new JsonObject { ["key"] = parameter.Key, ["value"] = parameter.Value?.DeepClone() })]);

        if (behavior.Variables.Length > 0)
        {
            root["variables"] = new JsonArray([.. behavior.Variables.Select(variable =>
            {
                var json = new JsonObject { ["name"] = variable.Name, ["type"] = Camel(variable.Type), ["scope"] = Camel(variable.Scope) };

                if (variable.Value is { } value)
                    json["value"] = value.DeepClone();

                return (JsonNode)json;
            })]);
        }

        if (behavior.Always.Length > 0)
            root["always"] = new JsonArray([.. behavior.Always.Select(state => (JsonNode)WriteState(state))]);

        if (behavior.Rules.Length > 0)
            root["rules"] = new JsonArray([.. behavior.Rules.Select(rule => (JsonNode)WriteRule(rule))]);

        root["states"] = new JsonArray([.. behavior.States.Select(state => (JsonNode)WriteState(state))]);
    }

    private static JsonObject WriteState(BehaviorState state)
    {
        var json = new JsonObject { ["id"] = state.Id, ["name"] = state.Name };

        if (state.When is { } when)
            json["when"] = WriteCondition(when);

        if (state.IsUrgent)
            json["urgent"] = true;

        // Forever is the file's old flag, so that a list written before repeats had a count reads and writes as it was.
        if (state.Repeat.Kind == RepeatKind.Forever)
            json["repeats"] = true;
        else if (WriteRepeat(state.Repeat) is { } repeat)
            json["repeat"] = repeat;

        if (state.States.Length > 0)
            json["states"] = new JsonArray([.. state.States.Select(child => (JsonNode)WriteState(child))]);

        if (state.Actions.Nodes.Length > 0)
            json["actions"] = WriteGraph(state.Actions);

        return json;
    }

    private static JsonObject WriteRule(BehaviorRule rule)
    {
        var json = new JsonObject { ["id"] = rule.Id, ["name"] = rule.Name, ["on"] = WriteCall(rule.On) };

        if (rule.When is { } when)
            json["when"] = WriteCondition(when);

        if (rule.Actions.Nodes.Length > 0)
            json["actions"] = WriteGraph(rule.Actions);

        return json;
    }

    private static JsonObject WriteCall(NodeCall call)
    {
        var json = new JsonObject { ["node"] = call.Node };

        if (call.Values.Count > 0)
            json["values"] = Values(call.Values);

        return json;
    }

    private static JsonObject WriteCondition(Condition condition)
    {
        return condition switch
        {
            AllCondition all => new JsonObject { ["all"] = new JsonArray([.. all.Conditions.Select(item => (JsonNode)WriteCondition(item))]) },
            AnyCondition any => new JsonObject { ["any"] = new JsonArray([.. any.Conditions.Select(item => (JsonNode)WriteCondition(item))]) },
            NotCondition not => new JsonObject { ["not"] = WriteCondition(not.Condition) },
            NodeCondition node => WriteCall(node.Call),
            _ => throw new ArgumentException($"No condition is a {condition.GetType().Name}.", nameof(condition)),
        };
    }

    private static JsonObject WriteGraph(ActionGraph graph)
    {
        var json = new JsonObject();
        var layout = new JsonObject();

        if (graph.Entry is { } entry)
            json["entry"] = entry;

        json["nodes"] = new JsonArray([.. graph.Nodes.Select(node =>
        {
            var item = new JsonObject { ["id"] = node.Id, ["node"] = node.Node };

            if (node.Values.Count > 0)
                item["values"] = Values(node.Values);

            if (node.IsDisabled)
                item["disabled"] = true;

            if (WriteRepeat(node.Repeat) is { } repeat)
                item["repeat"] = repeat;

            if (node.Position != default)
                layout[node.Id] = new JsonArray(node.Position.X, node.Position.Y);

            return (JsonNode)item;
        })]);

        if (graph.Links.Length > 0)
        {
            json["links"] = new JsonArray([.. graph.Links.Select(link =>
            {
                var item = new JsonObject { ["from"] = link.From, ["pin"] = link.Pin, ["to"] = link.To };

                if (link.Input is { } input)
                    item["input"] = input;

                return (JsonNode)item;
            })]);
        }

        // Where nodes stand is kept apart from what they do, so that moving one changes a part of the file of its own (D294).
        if (layout.Count > 0)
            json["layout"] = layout;

        return json;
    }
}
