using System.Text.Json.Nodes;
using D3B.Bot.Characters;

namespace D3B.Studio.Behaviors;

/// <summary>A character's variable as it was set.</summary>
/// <param name="Character">The character.</param>
/// <param name="Name">The variable's name.</param>
/// <param name="Value">Its value as JSON.</param>
public sealed record CharacterVariableChange(CharacterKey Character, string Name, JsonNode Value);
