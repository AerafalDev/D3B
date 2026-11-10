using System.Net;
using System.Text;
using D3B.Bot.Reports;

namespace D3B.Bot.Tests.Reports;

public sealed class ReportChannelTests
{
    private const string Url = "https://discord.com/api/webhooks/123456789012345678/TheSecretTokenOfTheTest";

    private static readonly Uri _asset = ReportChannel.AssetOf("AerafalDev/D3B");

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, List<Uri>? asked = null)
    {
        return new HttpClient(new FakeHandler(request =>
        {
            asked?.Add(request.RequestUri!);

            return respond(request);
        }));
    }

    private static HttpResponseMessage Text(string text, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "text/plain") };
    }

    [Fact]
    public void TheAssetIsOneOfTheReportChannelReleaseOfTheRepository()
    {
        Assert.Equal("https://github.com/AerafalDev/D3B/releases/download/report-channel/webhook.txt", _asset.ToString());
    }

    [Fact]
    public async Task TheChannelIsTheWebhookTheAssetHoldsAndItsLimitIsTheSinks()
    {
        var asked = new List<Uri>();
        var channel = new ReportChannel(Client(_ => Text(Url + "\n"), asked), _asset, maxFileBytes: 12345);
        var sink = await channel.GetSinkAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(sink);
        Assert.Equal(12345, sink.MaxFileBytes);
        Assert.Equal(_asset, Assert.Single(asked));

        // Read once: the next send does not ask again.
        await channel.GetSinkAsync(TestContext.Current.CancellationToken);

        Assert.Single(asked);
    }

    [Fact]
    public async Task AWebhookInTheEnvironmentStandsForTheAssetAndTheAssetIsNotRead()
    {
        var asked = new List<Uri>();
        var channel = new ReportChannel(Client(_ => Text("nope"), asked), _asset, Url);

        Assert.NotNull(await channel.GetSinkAsync(TestContext.Current.CancellationToken));
        Assert.Empty(asked);
    }

    [Theory]
    [InlineData("https://evil.example/api/webhooks/123456789012345678/abcdefgh")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task AnAssetThatIsNoDiscordWebhookGivesNoChannel(string text)
    {
        Assert.Null(await new ReportChannel(Client(_ => Text(text)), _asset).GetSinkAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnAssetTooLongOrMissingOrAnUnreachableGitHubGivesNoChannelAndIsAskedAgainNextTime()
    {
        Assert.Null(await new ReportChannel(Client(_ => Text(new string('a', 5000))), _asset).GetSinkAsync(TestContext.Current.CancellationToken));
        Assert.Null(await new ReportChannel(Client(_ => Text("gone", HttpStatusCode.NotFound)), _asset).GetSinkAsync(TestContext.Current.CancellationToken));

        var up = false;
        var channel = new ReportChannel(Client(_ => up ? Text(Url) : throw new HttpRequestException("no network")), _asset);

        Assert.Null(await channel.GetSinkAsync(TestContext.Current.CancellationToken));

        up = true;

        Assert.NotNull(await channel.GetSinkAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AWebhookThatIsGoneIsForgottenSoThatTheNextSendReadsTheAssetAgain()
    {
        var asked = new List<Uri>();
        var channel = new ReportChannel(
            Client(request => request.RequestUri == _asset ? Text(Url) : Text("""{"code":10015,"message":"Unknown Webhook"}""", HttpStatusCode.NotFound), asked),
            _asset);
        var directory = Path.Combine(Path.GetTempPath(), "D3B.Tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            var file = Path.Combine(directory, "D3B-K7QM-4X.d3breport");

            File.WriteAllBytes(file, [1]);

            var manifest = new ReportManifest { Code = "D3B-K7QM-4X", CreatedAt = DateTimeOffset.UnixEpoch, App = "1", Client = "3.7", Area = ReportArea.App, Files = [] };
            var sink = await channel.GetSinkAsync(TestContext.Current.CancellationToken);
            var result = await sink!.SendAsync(new ReportSendRequest(file, manifest, null), null, TestContext.Current.CancellationToken);

            Assert.Equal(ReportSendStatus.ChannelGone, result.Status);

            await channel.GetSinkAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, asked.Count(uri => uri == _asset));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TheLimitByDefaultIsTheOneMeasuredOnADiscordChannel()
    {
        Assert.Equal(20L * 1024 * 1024, DiscordReportSink.DefaultMaxFileBytes);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }
}
