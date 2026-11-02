using System.Text.Json.Nodes;

namespace D3B.Bot.Behaviors;

/// <summary>A variable a behavior declares (D298).</summary>
/// <param name="Name">Its name, unique in the behavior.</param>
/// <param name="Type">What it holds.</param>
/// <param name="Scope">How long it lives and who shares it.</param>
/// <param name="Value">Its value until set, as JSON; none, the type's own: off, 0 or empty.</param>
public sealed record BehaviorVariable(string Name, VariableType Type, VariableScope Scope, JsonNode? Value = null);
