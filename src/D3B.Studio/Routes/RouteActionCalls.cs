using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Settings;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Turns the actions a route's file holds into the values their nodes take, and back (D377).</summary>
/// <remarks>Only the values that differ from the node's defaults are written, so that a route's file stays short and a value the node adds later reads as its default.</remarks>
public static class RouteActionCalls
{
    /// <summary>Makes an action with the values its node starts from.</summary>
    /// <param name="definition">The action's node.</param>
    /// <returns>The call, with no value written.</returns>
    public static NodeCall Create(NodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new NodeCall(definition.Id, ImmutableSortedDictionary<string, JsonNode?>.Empty);
    }

    /// <summary>Reads an action's values.</summary>
    /// <param name="definition">The action's node.</param>
    /// <param name="call">The action, as the route's file holds it.</param>
    /// <returns>The values, and the settings whose stored value did not read.</returns>
    public static SettingsReading Read(NodeDefinition definition, NodeCall call)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(call);

        var values = new JsonObject();

        foreach (var (key, value) in call.Values)
            values[key] = value?.DeepClone();

        return definition.Values.Read(values.ToJsonString());
    }

    /// <summary>Makes the action of values.</summary>
    /// <param name="definition">The action's node.</param>
    /// <param name="settings">The values, a copy of the node's class.</param>
    /// <returns>The call, holding only the values that differ from the node's defaults.</returns>
    public static NodeCall Make(NodeDefinition definition, object? settings)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var written = JsonNode.Parse(definition.Values.Write(settings)) as JsonObject ?? [];
        var defaults = JsonNode.Parse(definition.Values.Write(definition.Values.CreateDefault())) as JsonObject ?? [];
        var values = ImmutableSortedDictionary.CreateBuilder<string, JsonNode?>(StringComparer.Ordinal);

        foreach (var (key, value) in written)
        {
            if (value is not null && !JsonNode.DeepEquals(value, defaults[key]))
                values[key] = value.DeepClone();
        }

        return new NodeCall(definition.Id, values.ToImmutable());
    }
}
