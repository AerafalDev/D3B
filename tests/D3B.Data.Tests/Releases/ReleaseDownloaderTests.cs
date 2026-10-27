using System.Security.Cryptography;
using D3B.Data.Releases;

namespace D3B.Data.Tests.Releases;

public sealed class ReleaseDownloaderTests : IDisposable
{
    private const string Repository = "AerafalDev/D3B";
    private const string Tag = "client-3.7.12.12";
    private const string Asset = "data-3.pack";

    private static readonly byte[] _content = [.. Enumerable.Range(0, 200_000).Select(i => (byte)(i * 31))];

    private readonly TemporaryDirectory _directory = new();
    private readonly FakeGitHub _github = new();
    private readonly ReleaseDownloader _downloader;

    private string Destination => _directory.Combine("3.7.12.12", Asset);

    public ReleaseDownloaderTests()
    {
        _downloader = new ReleaseDownloader(Repository, _github);
    }

    public void Dispose()
    {
        _downloader.Dispose();
        _github.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public async Task AnAssetLandsInPlaceOnceItMatchesItsChecksum()
    {
        _github.Publish(Repository, Tag, (Asset, _content));

        var hash = await _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(_content)), hash);
        Assert.Equal(_content, File.ReadAllBytes(Destination));
        Assert.Equal([Destination], Directory.GetFiles(Path.GetDirectoryName(Destination)!));
    }

    [Fact]
    public async Task TheTokenGoesToTheApiOnly()
    {
        _github.RequiredToken = "secret";
        _github.Publish(Repository, Tag, (Asset, _content));

        await _downloader.DownloadAsync(Tag, Asset, Destination, "secret", null, TestContext.Current.CancellationToken);

        Assert.Equal(3, _github.Requests.Count);
        Assert.All(_github.Requests, request => Assert.Equal(("api.github.com", "secret"), (request.Uri.Host, request.Token)));
    }

    [Fact]
    public async Task APrivateReleaseIsNotFoundWithoutAToken()
    {
        _github.RequiredToken = "secret";
        _github.Publish(Repository, Tag, (Asset, _content));

        var exception = await Assert.ThrowsAsync<ReleaseDownloadException>(() => _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));

        Assert.Contains("needs a GitHub token", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task AReleaseMissingTheChecksumIsReported()
    {
        _github.PublishAssets(Repository, Tag, (Asset, _content));

        var exception = await Assert.ThrowsAsync<ReleaseDownloadException>(() => _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));

        Assert.Contains(ClientRelease.ChecksumNameOf(Asset), exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task ADownloadThatDoesNotMatchItsChecksumIsDropped()
    {
        _github.PublishAssets(Repository, Tag, (Asset, _content), (ClientRelease.ChecksumNameOf(Asset), FakeGitHub.Checksum([1, 2, 3], Asset)));

        await Assert.ThrowsAsync<ReleaseDownloadException>(() => _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Destination)!));
    }

    [Fact]
    public async Task ADownloadTheCallerRejectsIsDropped()
    {
        _github.Publish(Repository, Tag, (Asset, _content));

        await Assert.ThrowsAsync<InvalidDataException>(() => _downloader.DownloadAsync(
            Tag,
            Asset,
            Destination,
            null,
            path => throw new InvalidDataException($"{path} is not wanted."),
            TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Destination)!));
    }

    [Fact]
    public async Task AFileThatLandsMeanwhileIsKept()
    {
        _github.Publish(Repository, Tag, (Asset, _content));

        var hash = await _downloader.DownloadAsync(Tag, Asset, Destination, null, _ => File.WriteAllBytes(Destination, [42]), TestContext.Current.CancellationToken);

        Assert.Null(hash);
        Assert.Equal([42], File.ReadAllBytes(Destination));
        Assert.Equal([Destination], Directory.GetFiles(Path.GetDirectoryName(Destination)!));
    }

    [Fact]
    public async Task AFileAlreadyInPlaceIsKeptWithoutAskingGitHub()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        File.WriteAllBytes(Destination, [42]);

        Assert.Null(await _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));
        Assert.Equal([42], File.ReadAllBytes(Destination));
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task ASecondDownloadOfTheSameFileStopsWhileTheFirstRuns()
    {
        _github.Publish(Repository, Tag, (Asset, _content));
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);

        using (new FileStream(Destination + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
        {
            var exception = await Assert.ThrowsAsync<ReleaseDownloadException>(() => _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));

            Assert.Contains("Another download", exception.Message, StringComparison.Ordinal);
        }

        await _downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken);

        Assert.Equal(_content, File.ReadAllBytes(Destination));
    }

    [Fact]
    public async Task ADownloadThatStallsFailsAndLeavesNothing()
    {
        _github.Publish(Repository, Tag, (Asset, _content));
        _github.StallingAsset = Asset;

        using var downloader = new ReleaseDownloader(Repository, _github, TimeSpan.FromMilliseconds(200));

        var exception = await Assert.ThrowsAsync<ReleaseDownloadException>(() => downloader.DownloadAsync(Tag, Asset, Destination, null, null, TestContext.Current.CancellationToken));

        Assert.Contains($"stopped sending {Asset}", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Destination)!));
    }
}
