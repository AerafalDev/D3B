namespace D3B.Hooks.Tests;

public sealed class HookInputTests
{
    [Fact]
    public void AFileToolsInputIsRead()
    {
        var input = HookInput.Parse("""{"cwd":"C:/p","tool_name":"Write","tool_input":{"file_path":"C:/p/a.cs","content":"x"}}""");

        Assert.Equal("C:/p", input.WorkingDirectory);
        Assert.Equal("C:/p/a.cs", input.FilePath);
        Assert.Null(input.Command);
        Assert.False(input.StopHookActive);
    }

    [Fact]
    public void ANotebooksPathIsItsFilePath()
    {
        var input = HookInput.Parse("""{"tool_input":{"notebook_path":"n.ipynb"}}""");

        Assert.Equal("n.ipynb", input.FilePath);
    }

    [Fact]
    public void AShellToolsCommandIsRead()
    {
        var input = HookInput.Parse("""{"tool_input":{"command":"git status"}}""");

        Assert.Equal("git status", input.Command);
    }

    [Fact]
    public void TheStopHooksFlagIsRead()
    {
        Assert.True(HookInput.Parse("""{"stop_hook_active":true}""").StopHookActive);
        Assert.False(HookInput.Parse("""{"stop_hook_active":false}""").StopHookActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    [InlineData("{}")]
    [InlineData("""{"tool_input":"not an object"}""")]
    [InlineData("""{"tool_input":{"file_path":3,"command":null},"cwd":[],"stop_hook_active":"yes"}""")]
    public void MissingOrMalformedFieldsReadAsAbsent(string json)
    {
        var input = HookInput.Parse(json);

        Assert.Null(input.FilePath);
        Assert.Null(input.Command);
        Assert.Null(input.WorkingDirectory);
        Assert.False(input.StopHookActive);
    }
}
