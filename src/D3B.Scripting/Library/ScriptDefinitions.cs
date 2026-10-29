namespace D3B.Scripting.Library;

/// <summary>The API's definitions D3B writes into the scripts' folder: <c>d3b.lua</c> and <c>.luarc.json</c>, for VS Code's Lua extension (D108).</summary>
/// <remarks>
/// <c>d3b.lua</c> holds the API as LuaCATS annotations, which LuaLS reads for documentation and completion; a test checks
/// that it documents every hook, field and function the API has. <c>.luarc.json</c> tells LuaLS the language is Lua 5.2
/// and which of its libraries a script lacks. Both are written as the host starts, when they differ, so that the folder
/// follows the API of the D3B that runs it.
/// </remarks>
internal static class ScriptDefinitions
{
    /// <summary>The definitions' file name, which the library does not list as a script.</summary>
    public const string FileName = "d3b.lua";

    /// <summary>LuaLS's settings file name.</summary>
    public const string SettingsName = ".luarc.json";

    /// <summary>Gets the definitions' text.</summary>
    public static string Lua { get; } = Resource("d3b.lua");

    /// <summary>Gets LuaLS's settings.</summary>
    public static string Settings { get; } = Resource("luarc.json");

    /// <summary>Writes both files into a folder, when they differ from what is there.</summary>
    public static void Write(string directory)
    {
        WriteIfChanged(Path.Combine(directory, FileName), Lua);
        WriteIfChanged(Path.Combine(directory, SettingsName), Settings);
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return;

        File.WriteAllText(path, text);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ScriptDefinitions).Assembly.GetManifestResourceStream($"D3B.Scripting.Definitions.{name}")
            ?? throw new InvalidOperationException($"The definitions' resource {name} is missing from the assembly.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
