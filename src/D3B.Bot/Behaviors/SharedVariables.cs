using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace D3B.Bot.Behaviors;

/// <summary>The variables the host's bots share, of the scope <see cref="VariableScope.Shared"/>, by name, until the host stops (D298).</summary>
/// <remarks>Step 10's groups read them; nothing keeps them from one run to the next.</remarks>
public sealed class SharedVariables
{
    private readonly ConcurrentDictionary<string, JsonNode> _values = new(StringComparer.Ordinal);

    /// <summary>Raised when a variable is set, by its name, from the thread that sets it: a handler must be quick, and must not throw.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>Gets a variable.</summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value as JSON, or <see langword="null"/> when it was never set.</returns>
    public JsonNode? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _values.TryGetValue(name, out var value) ? value.DeepClone() : null;
    }

    /// <summary>Sets a variable.</summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value as JSON.</param>
    public void Set(string name, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        _values[name] = value.DeepClone();
        Changed?.Invoke(this, name);
    }
}
