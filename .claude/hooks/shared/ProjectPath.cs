using System.Text.RegularExpressions;

namespace D3B.Hooks;

/// <summary>Paths as the hooks compare them: relative to the project, with forward slashes.</summary>
internal static partial class ProjectPath
{
    /// <summary>
    /// <paramref name="path"/> relative to <paramref name="root"/>, or <see langword="null"/> when it lies outside the project.
    /// </summary>
    public static string? Relative(string root, string path)
    {
        return Relative(root, path, OperatingSystem.IsWindows());
    }

    public static string? Relative(string root, string path, bool windows)
    {
        var baseDirectory = Path.GetFullPath(FromGitBash(root, windows));
        var target = Path.GetFullPath(Path.Combine(baseDirectory, FromGitBash(path, windows)));
        var inside = Path.GetRelativePath(baseDirectory, target).Replace('\\', '/');
        var outside = inside == ".." || inside.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(inside);
        return outside ? null : inside;
    }

    /// <summary>Git Bash writes <c>C:\x</c> as <c>/c/x</c>, which Windows would read as <c>C:\c\x</c>.</summary>
    public static string FromGitBash(string path, bool windows)
    {
        return windows ? GitBashDrive().Replace(path, "$1:/") : path;
    }

    [GeneratedRegex("^/([A-Za-z])/")]
    private static partial Regex GitBashDrive();
}
