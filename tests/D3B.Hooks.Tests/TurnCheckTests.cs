namespace D3B.Hooks.Tests;

public sealed class TurnCheckTests
{
    [Fact]
    public void ChangedFilesAreReadFromThePorcelainStatus()
    {
        const string porcelain = " M src/A.cs\n?? tests/B Tests/C.cs\nR  old.cs -> new.cs\nA  \"quoted name.cs\"\n";

        var files = TurnCheck.ChangedFiles(porcelain);

        Assert.Equal(["src/A.cs", "tests/B Tests/C.cs", "new.cs", "quoted name.cs"], files);
    }

    [Fact]
    public void AnEmptyStatusLeavesNoFile()
    {
        Assert.Empty(TurnCheck.ChangedFiles(string.Empty));
    }

    [Theory]
    [InlineData("src/D3B.Bot/Harvest.cs")]
    [InlineData("tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj")]
    [InlineData("Directory.Packages.props")]
    [InlineData("D3B.slnx")]
    [InlineData(".editorconfig")]
    [InlineData("global.json")]
    [InlineData("src/D3B.App/Views/Main.axaml")]
    [InlineData("src/D3B.Protocol/Game/fights.proto")]
    [InlineData("scripts/fights.cs")]
    public void ChangedCodeNeedsABuild(string file)
    {
        Assert.True(TurnCheck.NeedsBuild([file]));
    }

    [Theory]
    [InlineData("docs/decisions.md")]
    [InlineData("README.md")]
    [InlineData(".claude/skills/ship/SKILL.md")]
    [InlineData(".claude/settings.json")]
    [InlineData("artifacts/claude/last-green-build.txt")]
    [InlineData(".github/workflows/ci.yml")]
    public void ChangedDocsAndSettingsNeedNoBuild(string file)
    {
        Assert.False(TurnCheck.NeedsBuild([file]));
    }

    [Fact]
    public void AFingerprintChangesWithTheFilesContent()
    {
        using var folder = new TemporaryFolder();
        var path = folder.Write("A.cs", "one");
        var before = TurnCheck.Fingerprint(folder.Path, ["A.cs"]);

        File.WriteAllText(path, "three");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
        var after = TurnCheck.Fingerprint(folder.Path, ["A.cs"]);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void AFingerprintIsStableForTheSameFilesInAnyOrder()
    {
        using var folder = new TemporaryFolder();
        folder.Write("A.cs", "one");
        folder.Write("B.cs", "two");

        Assert.Equal(TurnCheck.Fingerprint(folder.Path, ["A.cs", "B.cs"]), TurnCheck.Fingerprint(folder.Path, ["B.cs", "A.cs"]));
    }

    [Fact]
    public void AFingerprintNoticesADeletedFile()
    {
        using var folder = new TemporaryFolder();
        var path = folder.Write("A.cs", "one");
        var before = TurnCheck.Fingerprint(folder.Path, ["A.cs"]);

        File.Delete(path);

        Assert.NotEqual(before, TurnCheck.Fingerprint(folder.Path, ["A.cs"]));
    }

    [Fact]
    public void AGreenBuildOfTheSameFilesIsNotRepeated()
    {
        Assert.False(TurnCheck.ShouldBuild(TurnCheck.GreenStamp("A1"), "A1", stopHookActive: false));
        Assert.False(TurnCheck.ShouldBuild(TurnCheck.GreenStamp("A1"), "A1", stopHookActive: true));
    }

    [Fact]
    public void ChangedFilesAreBuiltWhateverTheLastStampSays()
    {
        Assert.True(TurnCheck.ShouldBuild(TurnCheck.GreenStamp("A1"), "B2", stopHookActive: false));
        Assert.True(TurnCheck.ShouldBuild(TurnCheck.RedStamp("A1"), "B2", stopHookActive: true));
        Assert.True(TurnCheck.ShouldBuild(null, "B2", stopHookActive: false));
    }

    [Fact]
    public void ARedBuildIsRepeatedUntilClaudeIsToldAndChangesNothing()
    {
        Assert.True(TurnCheck.ShouldBuild(TurnCheck.RedStamp("A1"), "A1", stopHookActive: false));
        Assert.False(TurnCheck.ShouldBuild(TurnCheck.RedStamp("A1"), "A1", stopHookActive: true));
    }

    [Fact]
    public void ABuildThatFailsOnlyOnLockedFilesIsNotTheTurnsMistake()
    {
        const string output = "error MSB3027: Could not copy D3B.App.dll. Exceeded retry count of 10.\nerror MSB3021: Unable to copy file.\n";

        Assert.True(TurnCheck.IsOnlyLockedFiles(output));
    }

    [Fact]
    public void ALockedFileAmongOtherErrorsIsStillAFailure()
    {
        const string output = "error MSB3027: Could not copy D3B.App.dll.\nsrc/A.cs(3,1): error CS1002: ; expected\n";

        Assert.False(TurnCheck.IsOnlyLockedFiles(output));
    }

    [Fact]
    public void AnOutputWithoutErrorsIsNotALockedBuild()
    {
        Assert.False(TurnCheck.IsOnlyLockedFiles("Build FAILED.\n"));
    }

    [Fact]
    public void TheExcerptKeepsTheFirstDistinctLines()
    {
        const string output = "a\r\n\r\na\nb\nc\nd\n";

        Assert.Equal("a\nb\nc", TurnCheck.Excerpt(output, 3));
    }
}
