namespace D3B.Hooks.Tests;

public sealed class GitGuardTests
{
    [Theory]
    [InlineData("git push origin main")]
    [InlineData("git push origin HEAD:main")]
    [InlineData("git push origin HEAD:refs/heads/main")]
    [InlineData("git push origin feature:main")]
    [InlineData("git push --delete origin main")]
    [InlineData("git push origin :main")]
    [InlineData("git -C ../other push origin main")]
    [InlineData("cd src && git push origin main")]
    [InlineData("GIT_TRACE=1 git push origin main")]
    [InlineData("& \"C:\\Program Files\\Git\\cmd\\git.exe\" push origin main")]
    public void APushToMainIsRefused(string command)
    {
        var verdict = GitGuard.Check(command, "feature");

        Assert.Equal(HookDecision.Deny, verdict?.Decision);
    }

    [Theory]
    [InlineData("git push")]
    [InlineData("git push origin")]
    [InlineData("git push -u origin")]
    [InlineData("git push origin HEAD")]
    public void APushWithoutARefspecFollowsTheCurrentBranch(string command)
    {
        Assert.Equal(HookDecision.Deny, GitGuard.Check(command, "main")?.Decision);
        Assert.Equal(HookDecision.Ask, GitGuard.Check(command, "chore/claude-environment")?.Decision);
    }

    [Theory]
    [InlineData("git push --force origin feature")]
    [InlineData("git push -f origin feature")]
    [InlineData("git push -uf origin feature")]
    [InlineData("git push --force-with-lease origin feature")]
    [InlineData("git push origin +feature")]
    [InlineData("git push --all origin")]
    [InlineData("git push --mirror origin")]
    public void AForcedOrWholesalePushIsRefused(string command)
    {
        Assert.Equal(HookDecision.Deny, GitGuard.Check(command, "feature")?.Decision);
    }

    [Fact]
    public void APushOfAFeatureBranchAsksTheOwner()
    {
        var verdict = GitGuard.Check("git push -u origin feature", "feature");

        Assert.Equal(HookDecision.Ask, verdict?.Decision);
    }

    [Fact]
    public void AValueOfAPushOptionIsNotARefspec()
    {
        var verdict = GitGuard.Check("git push -o main origin feature", "feature");

        Assert.Equal(HookDecision.Ask, verdict?.Decision);
    }

    [Theory]
    [InlineData("git reset --hard")]
    [InlineData("git reset --hard HEAD~1")]
    [InlineData("git -C x reset --hard origin/main")]
    public void AHardResetIsRefused(string command)
    {
        Assert.Equal(HookDecision.Deny, GitGuard.Check(command, "feature")?.Decision);
    }

    [Theory]
    [InlineData("git clean -fd")]
    [InlineData("git clean --force")]
    [InlineData("git branch -D old")]
    public void DeletingUnsavedWorkAsksTheOwner(string command)
    {
        Assert.Equal(HookDecision.Ask, GitGuard.Check(command, "feature")?.Decision);
    }

    [Theory]
    [InlineData("gh pr create --title x")]
    [InlineData("gh pr merge 12 --squash")]
    [InlineData("gh release create client-3.7.13.17")]
    [InlineData("gh repo create D3B")]
    [InlineData("gh issue comment 4 --body x")]
    [InlineData("gh --repo AerafalDev/D3B pr merge 12")]
    public void PublishingOnGitHubAsksTheOwner(string command)
    {
        Assert.Equal(HookDecision.Ask, GitGuard.Check(command, "feature")?.Decision);
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git diff main...HEAD")]
    [InlineData("git log --oneline -5")]
    [InlineData("git commit -m \"fix: the push; git push origin main\"")]
    [InlineData("git reset --soft HEAD~1")]
    [InlineData("git clean -n")]
    [InlineData("git switch -c chore/x")]
    [InlineData("gh pr view 12")]
    [InlineData("gh pr checks 12")]
    [InlineData("gh issue list")]
    [InlineData("echo git push origin main")]
    [InlineData("dotnet build && dotnet test")]
    [InlineData("")]
    public void ReadingAndOrdinaryWorkPass(string command)
    {
        Assert.Null(GitGuard.Check(command, "feature"));
    }

    [Theory]
    [InlineData("git commit -m \"fix: x\" -m \"Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>\"")]
    [InlineData("git commit -m \"$(cat <<'EOF'\nfix: x\n\nCo-Authored-By: Claude <noreply@anthropic.com>\nEOF\n)\"")]
    [InlineData("git commit -m \"fix: x\" -m \"co-authored-by: claude\"")]
    [InlineData("gh pr create --title x --body \"Generated with [Claude Code](https://claude.com/claude-code)\"")]
    [InlineData("gh pr create --title x --body \"🤖 done\"")]
    [InlineData("gh pr comment 3 --body \"noreply@anthropic.com\"")]
    public void AnAIAttributionInACommitOrAPullRequestIsRefused(string command)
    {
        var verdict = GitGuard.Check(command, "feature");

        Assert.Equal(HookDecision.Deny, verdict?.Decision);
    }

    [Theory]
    [InlineData("git commit -m \"docs: describe the Claude Code setup\"")]
    [InlineData("git commit -m \"fix: x\" -m \"Co-Authored-By: Jane Doe <jane@example.org>\"")]
    [InlineData("echo \"Co-Authored-By: Claude <noreply@anthropic.com>\"")]
    [InlineData("git log --grep=\"Co-Authored-By: Claude\"")]
    public void TheWordsAloneAreNotAnAttribution(string command)
    {
        Assert.Null(GitGuard.Check(command, "feature"));
    }

    [Fact]
    public void AHeredocsBodyIsTextNotCommands()
    {
        const string command = "git commit -F - <<'EOF'\nfix: x\n\nit's the hook's rule\ngit reset --hard is refused\nEOF\n";

        Assert.Null(GitGuard.Check(command, "feature"));
    }

    [Fact]
    public void ACallAfterAHeredocIsStillSeen()
    {
        const string command = "git commit -F - <<'EOF'\nit's text\nEOF\ngit push origin main";

        Assert.Equal(HookDecision.Deny, GitGuard.Check(command, "feature")?.Decision);
    }

    [Fact]
    public void ARefusalBeatsAnAskInTheSameCommand()
    {
        var verdict = GitGuard.Check("git push origin feature; git push origin main", "feature");

        Assert.Equal(HookDecision.Deny, verdict?.Decision);
    }
}
