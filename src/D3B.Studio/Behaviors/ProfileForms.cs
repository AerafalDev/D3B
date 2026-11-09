using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Studio.Assets;

namespace D3B.Studio.Behaviors;

/// <summary>A profile as an activity's settings, for a behavior that plays one activity whole: the form the app's profile editor shows until the studio's (D306).</summary>
/// <remarks>
/// A behavior plays an activity whole when one node of it is an activity's, each of whose values is bound to the public
/// setting of its own key, as the given behaviors are (<see cref="GivenAssets"/>) and their copies. Its profile is then
/// the activity's settings, the behavior's values where the profile changes none; saved, it keeps only what differs (D300).
/// </remarks>
public static class ProfileForms
{
    private const string ParameterKey = "$param";

    /// <summary>Finds the activity a behavior plays whole, if it does.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="nodes">The kinds of nodes.</param>
    /// <returns>The activity, or <see langword="null"/> when the behavior is more than one activity played whole.</returns>
    public static ActivityDefinition? ActivityOf(BehaviorAsset behavior, NodeCatalog nodes)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(nodes);

        var found = behavior.Always.Concat(behavior.States).SelectMany(Nodes)
            .Select(node => (Node: node, Definition: nodes.TryGet(node.Node, out var definition) ? definition : null))
            .ToList();

        if (found is not [{ Definition.Activity: { } activity, Node: var single }] || behavior.Rules.Length > 0)
            return null;

        var keys = behavior.Parameters.Select(parameter => parameter.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var setting in activity.Settings.All)
        {
            if (!single.Values.TryGetValue(setting.Key, out var value) || Parameter(value) != setting.Key || !keys.Contains(setting.Key))
                return null;
        }

        return activity;
    }

    /// <summary>Gets a profile's settings, as the activity stores them: the profile's values, the behavior's where it changes none.</summary>
    /// <param name="behavior">The behavior, which plays the activity whole.</param>
    /// <param name="values">The values the profile changes; none for a new one.</param>
    /// <returns>The settings, as JSON.</returns>
    public static string SettingsOf(BehaviorAsset behavior, IReadOnlyDictionary<string, JsonNode?>? values)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var settings = new JsonObject();

        foreach (var parameter in behavior.Parameters)
            settings[parameter.Key] = (values is not null && values.TryGetValue(parameter.Key, out var changed) ? changed : parameter.Value)?.DeepClone();

        return settings.ToJsonString();
    }

    /// <summary>Gets what a profile changes of its behavior: the settings that differ from the behavior's values.</summary>
    /// <param name="behavior">The behavior, which plays the activity whole.</param>
    /// <param name="settings">The activity's settings, as JSON.</param>
    /// <returns>The values, by public setting.</returns>
    public static ImmutableSortedDictionary<string, JsonNode?> ValuesOf(BehaviorAsset behavior, string settings)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonNode.Parse(settings) as JsonObject ?? [];
        var values = ImmutableSortedDictionary.CreateBuilder<string, JsonNode?>(StringComparer.Ordinal);

        foreach (var parameter in behavior.Parameters)
        {
            if (json.TryGetPropertyValue(parameter.Key, out var value) && !JsonNode.DeepEquals(value, parameter.Value))
                values[parameter.Key] = value?.DeepClone();
        }

        return values.ToImmutable();
    }

    private static IEnumerable<GraphNode> Nodes(BehaviorState state)
    {
        return state.Actions.Nodes.Concat(state.States.SelectMany(Nodes));
    }

    private static string? Parameter(JsonNode? value)
    {
        return value is JsonObject { Count: 1 } bound && bound[ParameterKey] is JsonValue key && key.TryGetValue<string>(out var text) ? text : null;
    }
}
