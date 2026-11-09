using System.Text.Json;

namespace D3B.Hooks.Tests;

public sealed class HookResultTests
{
    [Fact]
    public void NoVerdictLetsTheCallRun()
    {
        var (code, output, error) = Report(null);

        Assert.Equal(0, code);
        Assert.Empty(output);
        Assert.Empty(error);
    }

    [Fact]
    public void ARefusalExitsWithTwoAndTellsClaudeWhy()
    {
        var (code, output, error) = Report(HookVerdict.Deny("not on main"));

        Assert.Equal(2, code);
        Assert.Empty(output);
        Assert.Equal("not on main", error.Trim());
    }

    [Fact]
    public void AnAskAnswersWithTheDecisionClaudeCodeReads()
    {
        var (code, output, error) = Report(HookVerdict.Ask("needs the owner's go"));

        using var document = JsonDocument.Parse(output);
        var answer = document.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal(0, code);
        Assert.Empty(error);
        Assert.Equal("PreToolUse", answer.GetProperty("hookEventName").GetString());
        Assert.Equal("ask", answer.GetProperty("permissionDecision").GetString());
        Assert.Equal("needs the owner's go", answer.GetProperty("permissionDecisionReason").GetString());
    }

    private static (int Code, string Output, string Error) Report(HookVerdict? verdict)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = HookResult.Report(verdict, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
