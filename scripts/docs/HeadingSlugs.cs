using System.Text;
using System.Text.RegularExpressions;

namespace D3B.Scripts.Docs;

/// <summary>The anchors GitHub gives the headings of a Markdown file.</summary>
internal static partial class HeadingSlugs
{
    /// <summary>Gives the anchor of a heading's text: in lowercase, punctuation removed, spaces as dashes.</summary>
    /// <param name="heading">The heading's text, after its <c>#</c> signs.</param>
    /// <returns>The anchor, without its <c>#</c>.</returns>
    public static string Of(string heading)
    {
        var text = InlineLink().Replace(heading.Trim(), "$1");
        var slug = new StringBuilder();

        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-')
                slug.Append(character);
            else if (character == ' ')
                slug.Append('-');
        }

        return slug.ToString();
    }

    /// <summary>Lists the anchors of the headings of a file's lines, outside the fenced code; a heading met twice gets <c>-1</c>, <c>-2</c>, as GitHub does.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>The anchors.</returns>
    public static HashSet<string> In(IReadOnlyList<string> lines)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var fenced = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;

                continue;
            }

            if (fenced || Heading().Match(line) is not { Success: true } heading)
                continue;

            var slug = Of(heading.Groups[1].Value);

            if (seen.TryGetValue(slug, out var count))
            {
                seen[slug] = count + 1;
                anchors.Add($"{slug}-{count}");
            }
            else
            {
                seen[slug] = 1;
                anchors.Add(slug);
            }
        }

        return anchors;
    }

    [GeneratedRegex(@"^#{1,6}\s+(.+?)\s*#*\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex InlineLink();
}
