using Lua;

namespace D3B.Scripting.Api;

/// <summary>Adds the script's API to its state: the <c>log</c> and <c>game</c> namespaces, and <c>print</c> to its console (D105).</summary>
/// <remarks>The <c>bot</c> is no global: the hooks receive it, so that a script's text, which runs before any hook, cannot act.</remarks>
internal static class ScriptApi
{
    /// <summary>The namespaces a script finds as globals, by name.</summary>
    public static IReadOnlyList<string> Namespaces { get; } = ["log", "game"];

    public static LuaTable Register(ScriptBinding binding)
    {
        var globals = binding.State.Environment;

        foreach (var space in new[] { LogNamespace.Create(binding), GameNamespace.Create(binding) })
            globals[space.Name] = space.Table;

        globals["print"] = new LuaFunction("print", (context, _) => new ValueTask<int>(LogNamespace.Write(new ScriptCall(context, null, "print", binding), ScriptLineKind.Message)));

        return BotObject.Create(binding);
    }
}
