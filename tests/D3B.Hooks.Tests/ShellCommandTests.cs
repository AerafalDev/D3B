namespace D3B.Hooks.Tests;

public sealed class ShellCommandTests
{
    [Fact]
    public void EachSimpleCommandOfAChainIsRead()
    {
        var invocations = ShellCommand.Invocations("git fetch && git status | cat; gh pr view 3");

        Assert.Equal(["git fetch", "git status", "gh pr"], invocations.Select(Describe));
    }

    [Fact]
    public void GitsGlobalOptionsAreSkippedWithTheirValues()
    {
        var invocation = Assert.Single(ShellCommand.Invocations("git -C ../x -c user.name=a --no-pager push origin main"));

        Assert.Equal("push", invocation.Verb);
        Assert.Equal(["origin", "main"], invocation.Arguments);
    }

    [Fact]
    public void AQuotedSeparatorDoesNotSplitTheCommand()
    {
        var invocations = ShellCommand.Invocations("git commit -m \"a; git push\" && echo 'x | y'");

        Assert.Equal("commit", Assert.Single(invocations).Verb);
    }

    [Fact]
    public void QuotesAreRemovedFromTheWords()
    {
        var invocation = Assert.Single(ShellCommand.Invocations("git commit -m 'two words'"));

        Assert.Equal(["-m", "two words"], invocation.Arguments);
    }

    [Theory]
    [InlineData("/usr/bin/git status")]
    [InlineData("C:\\Git\\cmd\\git.exe status")]
    [InlineData("& 'C:\\Program Files\\Git\\cmd\\git.exe' status")]
    [InlineData("GIT_PAGER=cat git status")]
    [InlineData("$(git status)")]
    public void TheToolIsFoundWhateverHowItIsWritten(string command)
    {
        var invocation = Assert.Single(ShellCommand.Invocations(command));

        Assert.Equal("git", invocation.Tool);
        Assert.Equal("status", invocation.Verb);
    }

    [Theory]
    [InlineData("echo git push")]
    [InlineData("dotnet run --file scripts/capture.cs")]
    [InlineData("git")]
    [InlineData("git -C x")]
    public void ACommandWithoutAGitOrGhCallHasNoInvocation(string command)
    {
        Assert.Empty(ShellCommand.Invocations(command));
    }

    private static string Describe(ShellInvocation invocation)
    {
        return $"{invocation.Tool} {invocation.Verb}";
    }
}
