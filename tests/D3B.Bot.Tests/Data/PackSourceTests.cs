using D3B.Bot.Data;
using D3B.Data.Releases;
using D3B.Data.Tests.Releases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Data;

public sealed class PackSourceTests : IDisposable
{
    private const string Tag = "client-3.7.12.12";

    private readonly TestInstall _install = new();
    private readonly FakeGitHub _github = new();
    private readonly ReleaseDownloader _downloader;

    public PackSourceTests()
    {
        _downloader = new ReleaseDownloader(ClientRelease.DefaultRepository, _github);
    }

    public void Dispose()
    {
        _downloader.Dispose();
        _install.Dispose();
    }

    [Fact]
    public async Task AMissingPackIsDownloadedFromTheReleaseOfItsBuild()
    {
        _github.Publish(ClientRelease.DefaultRepository, Tag, ("data-3.pack", TestInstall.Pack()));

        var source = Source(TestInstall.Token);

        Assert.True(await source.EnsureAsync(TestContext.Current.CancellationToken));

        using var pack = source.Open();

        Assert.Equal(TestInstall.Version, pack.Info.ClientVersion);
        Assert.All(_github.Requests, request => Assert.Equal(TestInstall.Token, request.Token));
    }

    [Fact]
    public async Task APackOnThisMachineIsNeverDownloaded()
    {
        _install.WritePack();

        Assert.False(await Source(TestInstall.Token).EnsureAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task AReleasedPackOfAnotherBuildIsRefused()
    {
        _github.Publish(ClientRelease.DefaultRepository, Tag, ("data-3.pack", TestInstall.Pack("3.7.11.1")));

        var exception = await Assert.ThrowsAsync<DataUnavailableException>(() => Source(TestInstall.Token).EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Contains("extracted from client 3.7.11.1", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_install.PackPath));
    }

    [Fact]
    public async Task AFailedDownloadNamesTheMissingBuildAndHowToExtractIt()
    {
        var exception = await Assert.ThrowsAsync<DataUnavailableException>(() => Source(TestInstall.Token).EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Contains($"No pack for client {TestInstall.Version}", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"has no release {Tag}", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/extract.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutATokenTheMessageSaysWhereOneComesFrom()
    {
        _github.RequiredToken = TestInstall.Token;
        _github.Publish(ClientRelease.DefaultRepository, Tag, ("data-3.pack", TestInstall.Pack()));

        var exception = await Assert.ThrowsAsync<DataUnavailableException>(() => Source(null).EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Releases:Token", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gh auth login", exception.Message, StringComparison.Ordinal);
        Assert.All(_github.Requests, request => Assert.Null(request.Token));
    }

    private PackSource Source(string? token)
    {
        var settings = DataSettings.Read(new ConfigurationBuilder().AddCommandLine(_install.Arguments).Build());

        // A command that does not exist stands for a machine without the GitHub CLI.
        return new PackSource(settings, _downloader, new ReleaseToken(token, "d3b-no-such-command"), NullLogger<PackSource>.Instance);
    }
}
