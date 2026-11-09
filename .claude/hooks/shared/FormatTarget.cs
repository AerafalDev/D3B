namespace D3B.Hooks;

/// <summary>Which files the format hook formats: the C# of the projects, not generated code nor build output.</summary>
internal static class FormatTarget
{
    private static readonly string[] _skippedFolders = ["artifacts/", "vendor/", "bin/", "obj/"];

    private static readonly string[] _generatedSuffixes = [".g.cs", ".generated.cs", ".Designer.cs"];

    public static bool IsFormatted(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var inSkippedFolder = _skippedFolders.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || path.Contains("/" + folder, StringComparison.OrdinalIgnoreCase));
        return !inSkippedFolder && !_generatedSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
