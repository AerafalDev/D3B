using System.Collections.Concurrent;
using D3B.Bot.Characters;

namespace D3B.Studio.Behaviors;

/// <summary>A run's variables, each of its scope: the run's own, the character's or the bots' (D298).</summary>
/// <remarks>Read and set from the run's steps and from the bot's loop, which plays the rules.</remarks>
internal sealed class BehaviorVariables
{
    private readonly Dictionary<string, BehaviorVariable> _declared;
    private readonly ConcurrentDictionary<string, object> _run = new(StringComparer.Ordinal);
    private readonly CharacterKey _character;
    private readonly CharacterVariables _characters;
    private readonly SharedVariables _shared;

    /// <summary>Raised when one of the run's variables is set, by the run or another.</summary>
    public event EventHandler? Changed;

    public BehaviorVariables(IEnumerable<BehaviorVariable> declared, CharacterKey character, CharacterVariables characters, SharedVariables shared)
    {
        _declared = declared.ToDictionary(variable => variable.Name, StringComparer.Ordinal);
        _character = character;
        _characters = characters;
        _shared = shared;

        foreach (var variable in _declared.Values.Where(variable => variable.Scope is VariableScope.Run))
            _run[variable.Name] = Initial(variable);
    }

    public object Get(string name)
    {
        var variable = _declared[name];

        return variable.Scope switch
        {
            VariableScope.Character => Read(variable, _characters.Get(_character, name)),
            VariableScope.Shared => Read(variable, _shared.Get(name)),
            _ => _run[name],
        };
    }

    public void Set(string name, object value)
    {
        var variable = _declared[name];

        switch (variable.Scope)
        {
            case VariableScope.Character:
                _characters.Set(_character, name, VariableValues.Write(value));
                break;
            case VariableScope.Shared:
                _shared.Set(name, VariableValues.Write(value));
                break;
            default:
                _run[name] = value;
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static object Initial(BehaviorVariable variable)
    {
        return VariableValues.TryRead(variable.Type, variable.Value, out var value) ? value : VariableValues.Default(variable.Type);
    }

    private static object Read(BehaviorVariable variable, System.Text.Json.Nodes.JsonNode? kept)
    {
        // Never set, or set by a behavior that gave the name another type: the declared value holds.
        return kept is not null && VariableValues.TryRead(variable.Type, kept, out var value) ? value : Initial(variable);
    }
}
