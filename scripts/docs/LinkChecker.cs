using System.Diagnostics;
using System.Text.RegularExpressions;

namespace D3B.Scripts.Docs;

/// <summary>Reads the Markdown files of the repository and finds the links to a file or a heading that does not exist.</summary>
internal static partial class LinkChecker
{
    /// <summary>Lists the Markdown files Git tracks or would track, relative to the repository.</summary>
    /// <param name="root">The repository's root.</param>
    /// <returns>The files' paths, with slashes.</returns>
    public static IReadOnlyList<string> MarkdownFiles(string root)
    {
        var start = new ProcessStartInfo("git", ["ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", "*.md"])
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
        };

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var output = git.StandardOutput.ReadToEnd();

        git.WaitForExit();

        if (git.ExitCode != 0)
            throw new InvalidOperationException($"git ls-files failed in {root}.");

        return [.. output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(file => File.Exists(Path.Combine(root, file)))];
    }

    /// <summary>Finds every link, in the given files, whose file or heading is missing.</summary>
    /// <param name="root">The repository's root.</param>
    /// <param name="files">The files to read, relative to the root.</param>
    /// <returns>The broken links, in the files' order.</returns>
    public static IReadOnlyList<BrokenLink> Check(string root, IReadOnlyList<string> files)
    {
        var anchors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var broken = new List<BrokenLink>();

        HashSet<string> AnchorsOf(string file)
        {
            if (!anchors.TryGetValue(file, out var found))
            {
                found = HeadingSlugs.In(File.ReadAllLines(Path.Combine(root, file)));
                anchors[file] = found;
            }

            return found;
        }

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(Path.Combine(root, file));
            var fenced = false;

            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;

                    continue;
                }

                if (fenced)
                    continue;

                foreach (Match link in Link().Matches(CodeSpan().Replace(lines[index], "")))
                {
                    var target = link.Groups[1].Value;

                    if (Problem(root, file, target, AnchorsOf) is { } reason)
                        broken.Add(new BrokenLink(file, index + 1, target, reason));
                }
            }
        }

        return broken;
    }

    private static string? Problem(string root, string file, string target, Func<string, HashSet<string>> anchorsOf)
    {
        if (target.Contains("://", StringComparison.Ordinal) || target.StartsWith("mailto:", StringComparison.Ordinal))
            return null;

        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var path = hash < 0 ? target : target[..hash];
        var anchor = hash < 0 ? "" : Uri.UnescapeDataString(target[(hash + 1)..]);
        var linked = path.Length == 0 ? file : Normalize(Path.Combine(Path.GetDirectoryName(file) ?? "", Uri.UnescapeDataString(path)));

        if (!File.Exists(Path.Combine(root, linked)) && !Directory.Exists(Path.Combine(root, linked)))
            return "the file does not exist";

        if (anchor.Length == 0 || !linked.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(root, linked)))
            return null;

        return anchorsOf(linked).Contains(anchor) || anchor.StartsWith('L') ? null : "no heading has this anchor";
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();

        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    [GeneratedRegex(@"\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"`[^`]*`")]
    private static partial Regex CodeSpan();
}
