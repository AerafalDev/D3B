using System.Text.RegularExpressions;

namespace D3B.Hooks;

/// <summary>
/// The explorer agent quotes the official client and the references in <c>vendor/</c>, which Git must never see, so it
/// writes only under <c>artifacts/studies/</c> and its shell only reads.
/// </summary>
internal static partial class StudyGuard
{
    private const string StudyFolder = "artifacts/studies/";

    public static string? PathRefusal(string? path)
    {
        if (path is not null && path.StartsWith(StudyFolder, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"The explorer writes only under {StudyFolder}, not {path ?? "outside the project"}.";
    }

    public static string? ShellRefusal(string command)
    {
        var kept = QuotedText().Replace(DiscardedOutput().Replace(command, string.Empty), "\"\"");
        if (kept.Contains('>') || Writers().IsMatch(kept))
        {
            return $"The explorer's shell only reads: write the study with Write or Edit, under {StudyFolder}.";
        }

        if (OtherTools().IsMatch(command))
        {
            return "The explorer runs only scripts/inspect.cs, scripts/capture.cs and scripts/fights.cs: the other tools write files or change the client's configuration.";
        }

        return null;
    }

    [GeneratedRegex(@"\d?>&\d|[*\d]?>\s*(/dev/null|\$null|nul\b)", RegexOptions.IgnoreCase)]
    private static partial Regex DiscardedOutput();

    [GeneratedRegex("\"[^\"]*\"|'[^']*'")]
    private static partial Regex QuotedText();

    [GeneratedRegex(@"dotnet\s+run\s+(--file\s+)?\S*scripts[/\\](?!(inspect|capture|fights)\.cs)\w+\.cs", RegexOptions.IgnoreCase)]
    private static partial Regex OtherTools();

    [GeneratedRegex(
        @"(^|[;&|(]\s*|\bxargs\s+|-exec\s+)(tee|cp|mv|rm|touch|dd|mkdir|copy-item|move-item|remove-item|set-content|add-content|out-file|new-item)\b|\bsed\s+-i|\bgit\s+(add|commit|push|checkout|switch|reset|stash|merge|rebase)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Writers();
}
