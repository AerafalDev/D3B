namespace D3B.Scripting;

/// <summary>A Lua script's text and the name its errors give, such as <c>mineur.lua</c>.</summary>
public sealed class LuaScript
{
    /// <summary>Gets the script's name: its errors read <c>name:line: what</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the script's text.</summary>
    public string Source { get; }

    /// <summary>Initializes a new instance of the <see cref="LuaScript"/> class.</summary>
    /// <param name="name">The script's name, such as its file's.</param>
    /// <param name="source">The script's text.</param>
    public LuaScript(string name, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        Name = name;
        Source = source;
    }
}
