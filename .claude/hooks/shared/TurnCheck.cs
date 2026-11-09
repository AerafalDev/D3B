using System.Security.Cryptography;
using System.Text;

namespace D3B.Hooks;

/// <summary>What the Stop hook decides from the files a turn changed and from the build's output.</summary>
internal static class TurnCheck
{
    private static readonly HashSet<string> _builtExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".props",
        ".targets",
        ".slnx",
        ".editorconfig",
        ".axaml",
        ".resx",
        ".proto",
    };

    private static readonly string[] _lockMarkers = ["MSB3021", "MSB3026", "MSB3027", "being used by another process"];

    /// <summary>The files <c>git status --porcelain</c> lists as changed or new, a renamed one by its new path.</summary>
    public static IReadOnlyList<string> ChangedFiles(string porcelain)
    {
        return porcelain
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 3)
            .Select(line => line[3..].Split(" -> ")[^1].Trim('"'))
            .ToList();
    }

    /// <summary>Whether a change to one of <paramref name="files"/> can break the build, whose analyzers hold the style.</summary>
    public static bool NeedsBuild(IEnumerable<string> files)
    {
        return files.Any(file =>
            !file.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
            && !file.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            && (_builtExtensions.Contains(Path.GetExtension(file)) || Path.GetFileName(file) is "global.json" or ".editorconfig"));
    }

    /// <summary>
    /// A fingerprint of the files with their size and time, so that a turn which changed nothing since the last green
    /// build does not build again.
    /// </summary>
    public static string Fingerprint(string root, IEnumerable<string> files)
    {
        var text = new StringBuilder();
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var info = new FileInfo(Path.Combine(root, file));
            text.Append(file).Append('|').Append(info.Exists ? info.Length : -1).Append('|').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : -1).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>
    /// Whether every error is a file another process holds, which is the app running from this checkout: it is not a
    /// mistake of the turn, and closing the app would disconnect the owner's clients.
    /// </summary>
    public static bool IsOnlyLockedFiles(string output)
    {
        var errors = output.Split('\n').Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase)).ToList();
        return errors.Count > 0 && errors.All(line => _lockMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    public static string GreenStamp(string fingerprint)
    {
        return "green " + fingerprint;
    }

    public static string RedStamp(string fingerprint)
    {
        return "red " + fingerprint;
    }

    /// <summary>
    /// Whether the turn's changes need a build. Not again after a green build of the same files; and not when Claude was
    /// already told this build fails and ended its turn without changing a file, which would only repeat the block.
    /// </summary>
    public static bool ShouldBuild(string? stamp, string fingerprint, bool stopHookActive)
    {
        if (stamp == GreenStamp(fingerprint))
        {
            return false;
        }

        return !(stopHookActive && stamp == RedStamp(fingerprint));
    }

    /// <summary>The first <paramref name="maxLines"/> distinct lines of the build's output.</summary>
    public static string Excerpt(string output, int maxLines)
    {
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).Distinct().Take(maxLines);
        return string.Join('\n', lines);
    }
}
