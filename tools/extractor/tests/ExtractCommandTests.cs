namespace D3B.Extractor.Tests;

public sealed class ExtractCommandTests
{
    [Fact]
    public void TheInstalledClientGivesADataFolderNamedAfterItsBuild()
    {
        var client = Installed.Client();
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "out");

        var (code, output, error) = Run("--client", client.Root, "--output", target);

        Assert.True(code == ExtractCommand.Done, error);
        Assert.Contains($"version {ClientBuild.Read(client).Version}", output, StringComparison.Ordinal);
        Assert.Contains("\"path\": \"areas.json\"", File.ReadAllText(Path.Combine(target, DataFolder.ManifestFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void AClientWithoutItsTablesExitsWithOneAndWritesNothing()
    {
        using var temporary = new TemporaryFolder();
        var client = temporary.Client("Version=3.7.13.17\nBuildDate=2026-11-03T14:09:52+00:00\n");
        var target = Path.Combine(temporary.Path, "out");

        var (code, output, error) = Run("--client", client, "--output", target);

        Assert.Equal(ExtractCommand.Failed, code);
        Assert.Contains("version 3.7.13.17, built 2026-11-03 14:09:52Z", output, StringComparison.Ordinal);
        Assert.Contains("texts", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
        Assert.False(Directory.Exists(target + ".d3b-staging"));
    }

    [Fact]
    public void AFolderWithoutClientExitsWithTwo()
    {
        using var temporary = new TemporaryFolder();

        var (code, _, error) = Run("--client", temporary.Path);

        Assert.Equal(ExtractCommand.NoClient, code);
        Assert.Contains("No client", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AClientWithAnUnreadableVersionExitsWithOne()
    {
        using var temporary = new TemporaryFolder();
        var client = temporary.Client("Version=three");

        var target = Path.Combine(temporary.Path, "out");

        var (code, _, error) = Run("--client", client, "--output", target);

        Assert.Equal(ExtractCommand.Failed, code);
        Assert.Contains("three", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void AnUnknownOptionExitsWithThree()
    {
        var (code, _, error) = Run("--clinet", "somewhere");

        Assert.Equal(ExtractCommand.Misused, code);
        Assert.Contains("--clinet", error, StringComparison.Ordinal);
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = ExtractCommand.Run(args, output, error);

        return (code, output.ToString(), error.ToString());
    }
}
