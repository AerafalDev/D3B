using System.Text.Json.Nodes;

namespace D3B.Hooks;

/// <summary>Turns a guard's verdict into what Claude Code reads from a hook: an exit code and, to ask, a JSON answer.</summary>
internal static class HookResult
{
    private const int Allowed = 0;
    private const int Refused = 2;

    public static int Report(HookVerdict? verdict, TextWriter output, TextWriter error)
    {
        if (verdict is null)
        {
            return Allowed;
        }

        if (verdict.Decision == HookDecision.Deny)
        {
            error.WriteLine(verdict.Reason);
            return Refused;
        }

        var answer = new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject
            {
                ["hookEventName"] = "PreToolUse",
                ["permissionDecision"] = "ask",
                ["permissionDecisionReason"] = verdict.Reason,
            },
        };
        output.WriteLine(answer.ToJsonString());
        return Allowed;
    }
}
