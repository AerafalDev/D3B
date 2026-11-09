using System.Text.RegularExpressions;

namespace D3B.Hooks;

/// <summary>
/// The Git and GitHub calls Claude may not make alone: nothing is pushed, published or merged without the owner's go, and
/// nothing rewrites the trunk or the work not yet committed.
/// </summary>
internal static partial class GitGuard
{
    private const string TrunkBranch = "main";

    private static readonly HashSet<string> _valuedPushOptions = ["-o", "--push-option", "--repo", "--receive-pack", "--exec"];

    private static readonly HashSet<string> _wholesalePushOptions = ["--all", "--mirror", "--branches"];

    private static readonly Dictionary<string, HashSet<string>> _gitHubWrites = new(StringComparer.Ordinal)
    {
        ["pr"] = ["create", "merge", "close", "reopen", "comment", "review", "edit", "ready"],
        ["issue"] = ["create", "comment", "close", "reopen", "edit", "delete"],
        ["release"] = ["create", "edit", "delete", "upload", "delete-asset"],
        ["repo"] = ["create", "delete", "edit", "fork", "rename", "archive"],
    };

    /// <summary>What to do with a shell <paramref name="command"/> run while <paramref name="branch"/> is checked out.</summary>
    public static HookVerdict? Check(string command, string branch)
    {
        var invocations = ShellCommand.Invocations(command);
        if (invocations.Any(PublishesText) && Attribution().IsMatch(command))
        {
            return HookVerdict.Deny("A commit or a pull request carries no AI attribution: no Co-Authored-By trailer naming Claude, no \"Generated with\" line. Remove it.");
        }

        HookVerdict? ask = null;
        foreach (var invocation in invocations)
        {
            var verdict = Judge(invocation, branch);
            if (verdict?.Decision == HookDecision.Deny)
            {
                return verdict;
            }

            ask ??= verdict;
        }

        return ask;
    }

    private static bool PublishesText(ShellInvocation invocation)
    {
        var first = invocation.Arguments.Count > 0 ? invocation.Arguments[0] : string.Empty;
        return invocation switch
        {
            { Tool: "git", Verb: "commit" } => true,
            { Tool: "gh", Verb: "pr" } => first is "create" or "edit" or "comment" or "review",
            { Tool: "gh", Verb: "issue" } => first is "create" or "comment" or "edit",
            _ => false,
        };
    }

    private static HookVerdict? Judge(ShellInvocation invocation, string branch)
    {
        var arguments = invocation.Arguments;
        if (invocation.Tool == "git")
        {
            return invocation.Verb switch
            {
                "push" => PushVerdict(arguments, branch),
                "reset" when arguments.Contains("--hard") => HookVerdict.Deny("git reset --hard is refused: it drops uncommitted work. Commit or stash it instead."),
                "clean" when arguments.Any(IsForceOption) => HookVerdict.Ask("git clean -f deletes untracked files, captures and databases among them."),
                "branch" when arguments.Contains("-D") => HookVerdict.Ask("git branch -D drops a branch that may not be merged."),
                _ => null,
            };
        }

        var isWrite = arguments.Count > 0 && _gitHubWrites.TryGetValue(invocation.Verb, out var verbs) && verbs.Contains(arguments[0]);
        return isWrite ? HookVerdict.Ask($"gh {invocation.Verb} {arguments[0]} publishes or changes something on GitHub: it needs the owner's go.") : null;
    }

    private static HookVerdict? PushVerdict(IReadOnlyList<string> arguments, string branch)
    {
        var positional = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (IsForceOption(argument) || _wholesalePushOptions.Contains(argument))
            {
                return HookVerdict.Deny($"git push {argument} is refused: it can overwrite the remote's history.");
            }

            if (_valuedPushOptions.Contains(argument))
            {
                index++;
            }
            else if (!argument.StartsWith('-'))
            {
                positional.Add(argument);
            }
        }

        var refspecs = positional.Skip(1).ToList();
        if (refspecs.Any(refspec => refspec.StartsWith('+')))
        {
            return HookVerdict.Deny("A forced refspec (+) is refused: it can overwrite the remote's history.");
        }

        var targets = refspecs.Count == 0 ? [branch] : refspecs.Select(refspec => Destination(refspec, branch)).ToList();
        if (targets.Contains(TrunkBranch))
        {
            return HookVerdict.Deny($"{TrunkBranch} is changed only through a merged pull request, never pushed to.");
        }

        return HookVerdict.Ask("A push needs the owner's go.");
    }

    private static bool IsForceOption(string argument)
    {
        return argument.StartsWith("--force", StringComparison.Ordinal) || (ShortOptions().IsMatch(argument) && argument.Contains('f'));
    }

    private static string Destination(string refspec, string branch)
    {
        var target = refspec.TrimStart('+').Split(':')[^1];
        var name = target is "HEAD" or "@" ? branch : target;
        return name.StartsWith("refs/heads/", StringComparison.Ordinal) ? name["refs/heads/".Length..] : name;
    }

    [GeneratedRegex("^-[a-zA-Z]+$")]
    private static partial Regex ShortOptions();

    [GeneratedRegex(@"co-authored-by:\s*claude|noreply@anthropic\.com|generated with \[?claude|🤖", RegexOptions.IgnoreCase)]
    private static partial Regex Attribution();
}
