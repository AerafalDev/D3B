using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Bot.Behaviors;

/// <summary>A node of a kind a module declared, with its values, as a condition or an event uses it (D298).</summary>
/// <param name="Node">The node's kind, by its id.</param>
/// <param name="Values">
/// Its values by key, as JSON: a value chosen, or one bound to a public setting (<c>{"$param": "key"}</c>) or a variable
/// (<c>{"$var": "name"}</c>); a value left out keeps the kind's default.
/// </param>
public sealed record NodeCall(string Node, ImmutableSortedDictionary<string, JsonNode?> Values);
