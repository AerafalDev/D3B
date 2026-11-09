namespace D3B.Hooks.Tests;

public sealed class StudyGuardTests
{
    [Fact]
    public void TheExplorerMayWriteItsStudies()
    {
        Assert.Null(StudyGuard.PathRefusal("artifacts/studies/fights.md"));
    }

    [Theory]
    [InlineData("docs/client.md")]
    [InlineData("artifacts/review/x.diff")]
    [InlineData("src/D3B.Engine/Fights/FightJudge.cs")]
    [InlineData(null)]
    public void TheExplorerMayWriteNothingElse(string? path)
    {
        Assert.NotNull(StudyGuard.PathRefusal(path));
    }

    [Theory]
    [InlineData("dotnet run --file scripts/inspect.cs -- summary")]
    [InlineData("git log --oneline -5")]
    [InlineData("grep -rn Fight vendor/ 2>/dev/null")]
    [InlineData("ls -la 2>&1")]
    [InlineData("Get-Content docs/client.md | Select-String Fight")]
    [InlineData("dotnet run --file scripts/capture.cs -- --frames > /dev/null")]
    [InlineData("grep -rn \"List<string>\" vendor/")]
    [InlineData("grep -rn rm vendor/")]
    [InlineData("dotnet run --file scripts/fights.cs -- --details")]
    public void ACommandThatOnlyReadsPasses(string command)
    {
        Assert.Null(StudyGuard.ShellRefusal(command));
    }

    [Theory]
    [InlineData("echo x > docs/client.md")]
    [InlineData("cat a >> b")]
    [InlineData("cp a b")]
    [InlineData("rm -rf src")]
    [InlineData("mkdir out")]
    [InlineData("sed -i s/a/b/ f")]
    [InlineData("Set-Content f x")]
    [InlineData("Out-File f")]
    [InlineData("git add -A")]
    [InlineData("git commit -m x")]
    [InlineData("ls | tee out.txt")]
    [InlineData("ls | xargs rm")]
    [InlineData("find . -name x -exec rm {} ;")]
    [InlineData("dotnet run --file scripts/client.cs -- restore")]
    [InlineData("dotnet run --file scripts/release.cs -- --publish")]
    [InlineData("dotnet run --file scripts/extract.cs")]
    public void ACommandThatWritesIsRefused(string command)
    {
        Assert.NotNull(StudyGuard.ShellRefusal(command));
    }
}
