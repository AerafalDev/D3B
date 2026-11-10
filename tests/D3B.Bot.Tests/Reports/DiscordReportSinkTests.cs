using System.Net;
using System.Text;
using System.Text.Json;
using D3B.Bot.Reports;
using D3B.Data.Tests;

namespace D3B.Bot.Tests.Reports;

public sealed class DiscordReportSinkTests : IDisposable
{
    private const string Url = "https://discord.com/api/webhooks/123456789012345678/TheSecretTokenOfTheTest";

    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static ReportManifest Manifest(ReportArea area = ReportArea.Fights)
    {
        return new ReportManifest
        {
            Code = "D3B-K7QM-4X",
            CreatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
            App = "0.8.1",
            Client = "3.7.13.17",
            Pack = "12",
            Mapping = "17",
            Area = area,
            Character = new ReportCharacter(11, 7, 80),
            Files = [],
        };
    }

    private ReportSendRequest Request(string? message, int size = 100, ReportArea area = ReportArea.Fights)
    {
        var path = _directory.Combine("D3B-K7QM-4X.d3breport");

        File.WriteAllBytes(path, new byte[size]);

        return new ReportSendRequest(path, Manifest(area), message);
    }

    private static DiscordReportSink Sink(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, long max = 10 * 1024 * 1024)
    {
        return new DiscordReportSink(new HttpClient(new FakeHandler(respond)), ReportWebhook.Parse(Url), max);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json = "{}")
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task AReportIsOnePostWithTheFileAndAnEmbedOfTheCodeTheAreaTheVersionsAndTheMessageAsItsText()
    {
        HttpRequestMessage? seen = null;
        JsonDocument? payload = null;
        var fileName = "";
        var fileLength = 0L;

        var sink = Sink(async request =>
        {
            seen = request;

            foreach (var part in (MultipartFormDataContent)request.Content!)
            {
                var name = part.Headers.ContentDisposition!.Name!.Trim('"');

                if (name == "payload_json")
                {
                    payload = JsonDocument.Parse(await part.ReadAsStringAsync());
                }
                else
                {
                    fileName = part.Headers.ContentDisposition.FileName!.Trim('"');
                    fileLength = (await part.ReadAsByteArrayAsync()).Length;
                }
            }

            return Json(HttpStatusCode.OK, """{"id":"998877"}""");
        });

        var result = await sink.SendAsync(Request("The fight froze.\nIt happened twice.", 321), null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSent);
        Assert.Equal("998877", result.MessageId);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.StartsWith(Url, seen.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("wait=true", seen.RequestUri.Query, StringComparison.Ordinal);
        Assert.Equal("D3B-K7QM-4X.d3breport", fileName);
        Assert.Equal(321, fileLength);

        var embed = payload!.RootElement.GetProperty("embeds")[0];

        Assert.Contains("D3B-K7QM-4X", embed.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Equal("The fight froze.\nIt happened twice.", embed.GetProperty("description").GetString());

        var fields = embed.GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("name").GetString()!, field => field.GetProperty("value").GetString()!);

        Assert.Equal("Fights", fields["Area"]);
        Assert.Contains("0.8.1", fields["App"], StringComparison.Ordinal);
        Assert.Contains("3.7.13.17", fields["Client"], StringComparison.Ordinal);
        Assert.Contains("pack 12", fields["Client"], StringComparison.Ordinal);
        Assert.Contains("11", fields["Character"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoMentionIsEverAllowedSoAMessageHoldingEveryoneOrARolePingsNobody()
    {
        JsonDocument? payload = null;

        var sink = Sink(async request =>
        {
            var part = ((MultipartFormDataContent)request.Content!).First(part => part.Headers.ContentDisposition!.Name!.Trim('"') == "payload_json");

            payload = JsonDocument.Parse(await part.ReadAsStringAsync());

            return Json(HttpStatusCode.OK);
        });

        await sink.SendAsync(Request("@everyone <@&123456> @here"), null, TestContext.Current.CancellationToken);

        var mentions = payload!.RootElement.GetProperty("allowed_mentions");

        Assert.Empty(mentions.GetProperty("parse").EnumerateArray());
        Assert.False(mentions.TryGetProperty("users", out _));
        Assert.Equal("@everyone <@&123456> @here", payload.RootElement.GetProperty("embeds")[0].GetProperty("description").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AReportWithoutAMessageSaysSo(string? message)
    {
        JsonDocument? payload = null;

        var sink = Sink(async request =>
        {
            var part = ((MultipartFormDataContent)request.Content!).First(part => part.Headers.ContentDisposition!.Name!.Trim('"') == "payload_json");

            payload = JsonDocument.Parse(await part.ReadAsStringAsync());

            return Json(HttpStatusCode.OK);
        });

        await sink.SendAsync(Request(message), null, TestContext.Current.CancellationToken);

        Assert.Equal("No message.", payload!.RootElement.GetProperty("embeds")[0].GetProperty("description").GetString());
    }

    [Fact]
    public async Task AReportOverTheLimitIsNotSentAtAllAndSaysSo()
    {
        var posted = false;
        var sink = Sink(_ =>
        {
            posted = true;

            return Task.FromResult(Json(HttpStatusCode.OK));
        }, max: 1000);

        var result = await sink.SendAsync(Request("x", 1001), null, TestContext.Current.CancellationToken);

        Assert.Equal(ReportSendStatus.TooLarge, result.Status);
        Assert.False(posted);

        Assert.True((await sink.SendAsync(Request("x", 1000), null, TestContext.Current.CancellationToken)).IsSent);
    }

    [Fact]
    public async Task TheChannelsAnswersAreResultsAndNeverCarryTheUrl()
    {
        var cases = new (HttpStatusCode Status, string Body, ReportSendStatus Expected)[]
        {
            (HttpStatusCode.RequestEntityTooLarge, "{}", ReportSendStatus.TooLarge),
            (HttpStatusCode.BadRequest, """{"code":40005,"message":"Request entity too large"}""", ReportSendStatus.TooLarge),
            (HttpStatusCode.NotFound, """{"code":10015,"message":"Unknown Webhook"}""", ReportSendStatus.ChannelGone),
            (HttpStatusCode.Unauthorized, "{}", ReportSendStatus.ChannelGone),
            (HttpStatusCode.BadRequest, """{"message":"Invalid Form Body"}""", ReportSendStatus.Rejected),
            (HttpStatusCode.InternalServerError, "oops", ReportSendStatus.Unavailable),
        };

        foreach (var (status, body, expected) in cases)
        {
            var result = await Sink(_ => Task.FromResult(Json(status, body))).SendAsync(Request("x"), null, TestContext.Current.CancellationToken);

            Assert.Equal(expected, result.Status);
            Assert.DoesNotContain("TheSecretTokenOfTheTest", result.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ARateLimitSaysHowLongToWait()
    {
        var result = await Sink(_ => Task.FromResult(Json(HttpStatusCode.TooManyRequests, """{"message":"You are being rate limited.","retry_after":2.5,"global":false}"""))).SendAsync(Request("x"), null, TestContext.Current.CancellationToken);

        Assert.Equal(ReportSendStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.RetryAfter);
    }

    [Fact]
    public async Task ANetworkThatFailsIsAResultNotAnException()
    {
        var down = await Sink(_ => throw new HttpRequestException("name resolution failed for discord.com/api/webhooks/123456789012345678/TheSecretTokenOfTheTest")).SendAsync(Request("x"), null, TestContext.Current.CancellationToken);

        Assert.Equal(ReportSendStatus.Unavailable, down.Status);
        Assert.DoesNotContain("TheSecretTokenOfTheTest", down.Detail, StringComparison.Ordinal);

        using var cancelled = new CancellationTokenSource();

        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sink(_ => Task.FromResult(Json(HttpStatusCode.OK))).SendAsync(Request("x"), null, cancelled.Token));
    }

    [Fact]
    public async Task TheProgressReachesTheSizeOfTheFile()
    {
        var told = new List<long>();
        var sink = Sink(async request =>
        {
            // Reading the body is what sends it.
            await request.Content!.ReadAsByteArrayAsync();

            return Json(HttpStatusCode.OK);
        });

        await sink.SendAsync(Request("x", 200_000), new Progress<long>(told.Add), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.NotEmpty(told);
        Assert.Equal(200_000, told.Max());
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/123456789012345678/abc-DEF_123")]
    [InlineData("https://discordapp.com/api/webhooks/123456789012345678/abc-DEF_123")]
    [InlineData("  https://discord.com/api/webhooks/123456789012345678/abc-DEF_123\n")]
    public void AWebhookUrlOfDiscordIsRead(string text)
    {
        Assert.True(ReportWebhook.TryParse(text, out var webhook));
        Assert.StartsWith("https://", webhook!.Url.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://discord.com/api/webhooks/123456789012345678/abc")]
    [InlineData("https://evil.example/api/webhooks/123456789012345678/abc")]
    [InlineData("https://discord.com.evil.example/api/webhooks/123456789012345678/abc")]
    [InlineData("https://discord.com/api/webhooks/123/abc?x=1")]
    [InlineData("https://user@discord.com/api/webhooks/123456789012345678/abc")]
    [InlineData("https://discord.com/api/webhooks/notanumber/abc")]
    [InlineData("https://discord.com/other")]
    public void AnythingButAWebhookUrlOfDiscordIsRefusedSoThatANewAppCannotBeSentSomewhereElse(string text)
    {
        Assert.False(ReportWebhook.TryParse(text, out _));
    }

    [Fact]
    public void AWebhooksToStringHidesItsToken()
    {
        Assert.DoesNotContain("TheSecretTokenOfTheTest", ReportWebhook.Parse(Url).ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return _respond(request);
        }
    }
}
