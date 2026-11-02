using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using D3B.Bot.Characters;

namespace D3B.Bot.Behaviors;

/// <summary>The variables each character keeps, of the scope <see cref="VariableScope.Character"/>, by name, one per host (D298).</summary>
/// <remarks>
/// D3B's database keeps them from one run to the next, when the host has it: it takes them back before the host starts
/// (<see cref="Remember"/>) and writes each as it changes (<see cref="Changed"/>). Values are kept as JSON, so that two
/// behaviors that give one name different types read each its own type's default rather than throw.
/// </remarks>
public sealed class CharacterVariables
{
    private readonly ConcurrentDictionary<(CharacterKey Character, string Name), JsonNode> _values = new();

    /// <summary>Raised when a variable is set, from the thread that sets it: a handler must be quick, and must not throw.</summary>
    public event EventHandler<CharacterVariableChange>? Changed;

    /// <summary>Gets every variable kept, by character and name.</summary>
    /// <returns>The variables, their values as JSON.</returns>
    public IReadOnlyList<CharacterVariableChange> All()
    {
        return [.. _values.Select(entry => new CharacterVariableChange(entry.Key.Character, entry.Key.Name, entry.Value.DeepClone()))];
    }

    /// <summary>Gets a character's variable.</summary>
    /// <param name="character">The character.</param>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value as JSON, or <see langword="null"/> when it was never set.</returns>
    public JsonNode? Get(CharacterKey character, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _values.TryGetValue((character, name), out var value) ? value.DeepClone() : null;
    }

    /// <summary>Sets a character's variable.</summary>
    /// <param name="character">The character.</param>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value as JSON.</param>
    public void Set(CharacterKey character, string name, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        _values[(character, name)] = value.DeepClone();
        Changed?.Invoke(this, new CharacterVariableChange(character, name, value.DeepClone()));
    }

    /// <summary>Takes back a variable D3B's database kept, raising nothing.</summary>
    /// <param name="character">The character.</param>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value as JSON.</param>
    public void Remember(CharacterKey character, string name, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        _values[(character, name)] = value.DeepClone();
    }
}
