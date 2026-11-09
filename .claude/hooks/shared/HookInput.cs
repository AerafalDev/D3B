using System.Text.Json;

namespace D3B.Hooks;

/// <summary>The fields of Claude Code's hook input the project's hooks read; the rest of the JSON is ignored.</summary>
internal sealed class HookInput
{
    public string? WorkingDirectory { get; private init; }

    public string? FilePath { get; private init; }

    public string? Command { get; private init; }

    public bool StopHookActive { get; private init; }

    public static HookInput Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HookInput();
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tool = Child(root, "tool_input");
        return new HookInput
        {
            WorkingDirectory = Text(root, "cwd"),
            FilePath = Text(tool, "file_path") ?? Text(tool, "notebook_path"),
            Command = Text(tool, "command"),
            StopHookActive = Flag(root, "stop_hook_active"),
        };
    }

    public static HookInput ReadStandardInput()
    {
        return Parse(Console.In.ReadToEnd());
    }

    /// <summary>The project's root: Claude Code's variable first, since a hook may run from a subfolder.</summary>
    public string ProjectDirectory()
    {
        return Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ?? WorkingDirectory ?? Directory.GetCurrentDirectory();
    }

    private static JsonElement Child(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var child) ? child : default;
    }

    private static string? Text(JsonElement parent, string name)
    {
        var child = Child(parent, name);
        return child.ValueKind == JsonValueKind.String ? child.GetString() : null;
    }

    private static bool Flag(JsonElement parent, string name)
    {
        return Child(parent, name).ValueKind == JsonValueKind.True;
    }
}
