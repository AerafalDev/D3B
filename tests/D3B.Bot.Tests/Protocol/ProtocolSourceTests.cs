using D3B.Bot.Data;
using D3B.Bot.Protocol;
using D3B.Data;
using D3B.Data.Releases;
using D3B.Data.Tests.Releases;
using D3B.Protocol.Connection;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Protocol;

public sealed class ProtocolSourceTests : IDisposable
{
    private const string Tag = "client-3.7.12.12";

    private readonly TestInstall _install = new();
    private readonly FakeGitHub _github = new();
    private readonly ReleaseDownloader _downloader;

    public ProtocolSourceTests()
    {
        _downloader = new ReleaseDownloader(ClientRelease.DefaultRepository, _github);
    }

    public void Dispose()
    {
        _downloader.Dispose();
        _install.Dispose();
    }

    [Fact]
    public async Task AMissingSchemaAndMappingAreDownloadedFromTheReleaseOfTheirBuild()
    {
        var (schema, mapping) = TestInstall.Protocol();

        _github.Publish(ClientRelease.DefaultRepository, Tag, ("schema-1.binpb", schema), ("mapping-1.binpb", mapping));

        var source = Source(TestInstall.Token);

        Assert.True(await source.EnsureAsync(TestContext.Current.CancellationToken));

        var translation = source.Load();

        Assert.Equal(TestInstall.Version, translation.ClientVersion);
        Assert.Equal(schema, File.ReadAllBytes(_install.SchemaPath));
        Assert.All(_github.Requests, request => Assert.Equal(TestInstall.Token, request.Token));
    }

    [Fact]
    public async Task ASchemaAndMappingOnThisMachineAreNeverDownloaded()
    {
        _install.WriteProtocol();

        var source = Source(TestInstall.Token);

        Assert.False(await source.EnsureAsync(TestContext.Current.CancellationToken));
        Assert.Equal(TestInstall.Version, source.Load().ClientVersion);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task AReleasedMappingOfAnotherBuildIsRefused()
    {
        var (schema, mapping) = TestInstall.Protocol("3.7.11.1");

        _github.Publish(ClientRelease.DefaultRepository, Tag, ("schema-1.binpb", schema), ("mapping-1.binpb", mapping));

        var exception = await Assert.ThrowsAsync<ProtocolUnavailableException>(() => Source(TestInstall.Token).EnsureAsync(TestContext.Current.CancellationToken));

        Assert.Contains("maps client 3.7.11.1", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_install.MappingPath));
    }

    [Fact]
    public async Task WithoutAReleaseTheMessageSaysHowToMapTheBuild()
    {
        var exception = await Assert.ThrowsAsync<ProtocolUnavailableException>(() => Source(TestInstall.Token).EnsureAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith($"No schema for client {TestInstall.Version}", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"has no release {Tag}", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/schema.cs -- ", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"then dotnet run --file scripts/mapping.cs -- --client \"{_install.ClientDirectory}\" --data \"{_install.PacksDirectory}\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingThatDoesNotHoldIsRefused()
    {
        _install.WriteProtocol(edit: mapping => mapping.Messages.Remove(mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName)));

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source(TestInstall.Token).Load());

        Assert.StartsWith($"The mapping of client {TestInstall.Version} does not hold: ", exception.Message, StringComparison.Ordinal);
        Assert.Contains("d3b.connection.Ping has no counterpart.", Assert.IsType<ProtocolMismatchException>(exception.InnerException).Failures);
        Assert.Contains("dotnet run --file scripts/mapping.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingMatchedAgainstAnotherSchemaIsRefused()
    {
        _install.WriteProtocol(edit: mapping => mapping.SchemaSha256 = Google.Protobuf.ByteString.CopyFrom(new byte[32]));

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source(TestInstall.Token).Load());

        Assert.Contains("The mapping was matched against another schema.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingOfAnotherBuildOnThisMachineIsRefused()
    {
        _install.WriteProtocol("3.7.11.1");

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source(TestInstall.Token).Load());

        Assert.Contains($"maps client 3.7.11.1, not {TestInstall.Version}", exception.Message, StringComparison.Ordinal);
    }

    private ProtocolSource Source(string? token)
    {
        var settings = DataSettings.Read(new ConfigurationBuilder().AddCommandLine(_install.Arguments).Build());

        // A command that does not exist stands for a machine without the GitHub CLI.
        return new ProtocolSource(settings, new ClientBuild(TestInstall.Version, null), _downloader, new ReleaseToken(token, "d3b-no-such-command"), NullLogger<ProtocolSource>.Instance);
    }
}
