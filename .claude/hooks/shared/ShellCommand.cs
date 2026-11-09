using System.Text;
using System.Text.RegularExpressions;

namespace D3B.Hooks;

/// <summary>
/// Reads the <c>git</c> and <c>gh</c> calls of a Bash or PowerShell command. It is a safety net against a mistake, not a
/// security boundary: a call wrapped in <c>bash -c "..."</c> is not seen.
/// </summary>
internal static partial class ShellCommand
{
    private static readonly HashSet<string> _valuedGitOptions =
    [
        "-C",
        "-c",
        "--git-dir",
        "--work-tree",
        "--namespace",
        "--exec-path",
        "--config-env",
    ];

    private static readonly HashSet<string> _valuedGhOptions = ["-R", "--repo"];

    public static IReadOnlyList<ShellInvocation> Invocations(string command)
    {
        var invocations = new List<ShellInvocation>();
        foreach (var tokens in Segments(WithoutHeredocBodies(command)))
        {
            var invocation = Invocation(tokens);
            if (invocation is not null)
            {
                invocations.Add(invocation);
            }
        }

        return invocations;
    }

    /// <summary>
    /// A heredoc's body is text for the command, a commit message for instance, not commands: its apostrophes would
    /// otherwise open quotes that swallow the rest, and a line of prose that starts with <c>git</c> would be read as a call.
    /// </summary>
    private static string WithoutHeredocBodies(string command)
    {
        var kept = new List<string>();
        string? terminator = null;
        foreach (var line in command.Split('\n'))
        {
            if (terminator is not null)
            {
                terminator = line.Trim() == terminator ? null : terminator;
                continue;
            }

            kept.Add(line);
            var heredoc = Heredoc().Match(line);
            terminator = heredoc.Success ? heredoc.Groups[2].Value : null;
        }

        return string.Join('\n', kept);
    }

    /// <summary>The words of each simple command, split on <c>; | &amp; ( )</c> and newlines outside quotes.</summary>
    private static List<List<string>> Segments(string command)
    {
        var segments = new List<List<string>>();
        var tokens = new List<string>();
        var token = new StringBuilder();
        var started = false;
        var quote = '\0';

        void EndToken()
        {
            if (started)
            {
                tokens.Add(token.ToString());
            }

            token.Clear();
            started = false;
        }

        void EndSegment()
        {
            EndToken();
            if (tokens.Count > 0)
            {
                segments.Add(tokens);
                tokens = [];
            }
        }

        foreach (var character in command)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    token.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
                started = true;
            }
            else if (character is ';' or '|' or '&' or '(' or ')' or '\n' or '\r')
            {
                EndSegment();
            }
            else if (char.IsWhiteSpace(character))
            {
                EndToken();
            }
            else
            {
                token.Append(character);
                started = true;
            }
        }

        EndSegment();
        return segments;
    }

    private static ShellInvocation? Invocation(List<string> tokens)
    {
        var start = 0;
        while (start < tokens.Count && EnvironmentAssignment().IsMatch(tokens[start]))
        {
            start++;
        }

        var tool = start < tokens.Count ? ToolOf(tokens[start]) : null;
        if (tool is null)
        {
            return null;
        }

        var valued = tool == "git" ? _valuedGitOptions : _valuedGhOptions;
        var index = start + 1;
        while (index < tokens.Count && tokens[index].StartsWith('-'))
        {
            index += valued.Contains(tokens[index]) ? 2 : 1;
        }

        return index < tokens.Count ? new ShellInvocation(tool, tokens[index], tokens.GetRange(index + 1, tokens.Count - index - 1)) : null;
    }

    private static string? ToolOf(string token)
    {
        var name = token.Split('\\', '/')[^1].ToLowerInvariant();
        name = name.EndsWith(".exe", StringComparison.Ordinal) ? name[..^4] : name;
        return name is "git" or "gh" ? name : null;
    }

    [GeneratedRegex("""(?<!<)<<(?!<)-?\s*(["']?)([A-Za-z_]\w*)\1""")]
    private static partial Regex Heredoc();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*=")]
    private static partial Regex EnvironmentAssignment();
}
