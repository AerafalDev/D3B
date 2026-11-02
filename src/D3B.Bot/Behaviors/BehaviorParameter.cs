using System.Text.Json.Nodes;

namespace D3B.Bot.Behaviors;

/// <summary>A public setting of a behavior, which its nodes' values bind to and its profiles change (D296, D300).</summary>
/// <param name="Key">Its key, unique in the behavior, which values bind to as <c>{"$param": "key"}</c>.</param>
/// <param name="Value">Its value in the behavior, as JSON, which a profile that does not change it follows.</param>
public sealed record BehaviorParameter(string Key, JsonNode? Value);
