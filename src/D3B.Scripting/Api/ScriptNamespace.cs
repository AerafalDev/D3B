using Lua;

namespace D3B.Scripting.Api;

/// <summary>A namespace of the script's API, such as <c>map</c>: a table of functions, each called with its arguments read through <see cref="ScriptCall"/>.</summary>
internal sealed class ScriptNamespace
{
    private readonly ScriptBinding _binding;

    /// <summary>Gets the namespace's name, its global's.</summary>
    public string Name { get; }

    /// <summary>Gets the namespace's table.</summary>
    public LuaTable Table { get; } = new();

    public ScriptNamespace(string name, ScriptBinding binding)
    {
        Name = name;
        _binding = binding;
    }

    /// <summary>Adds a function that waits for nothing.</summary>
    public ScriptNamespace Add(string name, Func<ScriptCall, int> body)
    {
        var function = $"{Name}:{name}";
        var table = Table;
        var binding = _binding;

        Table[name] = new LuaFunction(function, (context, _) => new ValueTask<int>(body(new ScriptCall(context, table, function, binding))));

        return this;
    }

    /// <summary>Adds a function that waits, such as for a behavior of the bot; the step's token cancels it.</summary>
    public ScriptNamespace Add(string name, Func<ScriptCall, CancellationToken, Task<int>> body)
    {
        var function = $"{Name}:{name}";
        var table = Table;
        var binding = _binding;

        Table[name] = new LuaFunction(function, async (context, cancellationToken) => await body(new ScriptCall(context, table, function, binding), cancellationToken).ConfigureAwait(false));

        return this;
    }
}
