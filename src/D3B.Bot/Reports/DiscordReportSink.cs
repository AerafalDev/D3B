using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace D3B.Bot.Reports;

/// <summary>Posts a report to a private Discord webhook: one multipart post, the report as the file and an embed of its code, area, versions and message (D410, D411).</summary>
/// <remarks>
/// <para>
/// The post forbids every mention (<c>allowed_mentions</c> with an empty <c>parse</c>), so that a message holding <c>@everyone</c> pings no one. The
/// message is the embed's text, as typed. A report over <see cref="MaxFileBytes"/> is not posted. Every failure is a <see cref="ReportSendResult"/> whose
/// detail never holds the webhook's URL, which is a secret.
/// </para>
/// <para>A webhook cannot rate-limit a player nor be told from another poster: whoever reads its URL can post to it. The maintainer's tool reads what arrives as untrusted input.</para>
/// </remarks>
public sealed class DiscordReportSink : IReportSink
{
    /// <summary>The most bytes of a file by default: 20 MiB. Measured on 2026-11-10 on the owner's webhook with files of random bytes: 20 MiB was taken and 21.5 MiB refused (the post's size limit is not a published number, and it depends on the server's level).</summary>
    public const long DefaultMaxFileBytes = 20L * 1024 * 1024;

    private const int TooLargeCode = 40005;

    private readonly HttpClient _http;
    private readonly ReportWebhook _webhook;

    /// <summary>Initializes a new instance of the <see cref="DiscordReportSink"/> class.</summary>
    /// <param name="http">The client that posts.</param>
    /// <param name="webhook">The webhook.</param>
    /// <param name="maxFileBytes">The most bytes of a file the channel takes.</param>
    public DiscordReportSink(HttpClient http, ReportWebhook webhook, long maxFileBytes = DefaultMaxFileBytes)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(webhook);

        _http = http;
        _webhook = webhook;
        MaxFileBytes = maxFileBytes;
    }

    /// <inheritdoc/>
    public long MaxFileBytes { get; }

    /// <inheritdoc/>
    public async Task<ReportSendResult> SendAsync(ReportSendRequest request, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var length = new FileInfo(request.FilePath).Length;

        if (length > MaxFileBytes)
            return new ReportSendResult(ReportSendStatus.TooLarge, $"The report is {length:N0} bytes and the channel takes {MaxFileBytes:N0}.");

        try
        {
            await using var file = new ProgressStream(File.OpenRead(request.FilePath), progress);
            using var content = new MultipartFormDataContent();
            using var attachment = new StreamContent(file);

            attachment.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(new StringContent(Payload(request), Encoding.UTF8, "application/json"), "payload_json");
            content.Add(attachment, "files[0]", request.Manifest.Code + ".d3breport");

            // Waited for, so that a refusal is told and the post's id is known.
            var target = new UriBuilder(_webhook.Url) { Query = "wait=true" }.Uri;

            using var response = await _http.PostAsync(target, content, cancellationToken).ConfigureAwait(false);

            return await ResultOfAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return new ReportSendResult(ReportSendStatus.Unavailable, ReportWebhook.Hide(exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ReportSendResult(ReportSendStatus.Unavailable, "The channel did not answer in time.");
        }
        catch (IOException exception)
        {
            return new ReportSendResult(ReportSendStatus.Unavailable, ReportWebhook.Hide(exception.Message));
        }
    }

    private static string Payload(ReportSendRequest request)
    {
        var manifest = request.Manifest;
        var character = manifest.Character is { } who
            ? string.Create(CultureInfo.InvariantCulture, $"{who.Id} (breed {who.Breed?.ToString(CultureInfo.InvariantCulture) ?? "?"}, level {who.Level?.ToString(CultureInfo.InvariantCulture) ?? "?"})")
            : "none";

        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteStartObject("allowed_mentions");
            json.WriteStartArray("parse");
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteStartArray("embeds");
            json.WriteStartObject();
            json.WriteString("title", "Report " + manifest.Code);
            json.WriteString("description", string.IsNullOrEmpty(request.Message) ? "No message." : request.Message);
            json.WriteString("timestamp", manifest.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            json.WriteStartArray("fields");
            Field(json, "Area", manifest.Area.ToString());
            Field(json, "App", manifest.App);
            Field(json, "Client", $"{manifest.Client} · pack {manifest.Pack ?? "?"}, mapping {manifest.Mapping ?? "?"}");
            Field(json, "Character", character);
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Field(Utf8JsonWriter json, string name, string value)
    {
        json.WriteStartObject();
        json.WriteString("name", name);
        json.WriteString("value", value);
        json.WriteBoolean("inline", true);
        json.WriteEndObject();
    }

    private static async Task<ReportSendResult> ResultOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        var status = response.StatusCode;

        if (response.IsSuccessStatusCode)
            return new ReportSendResult(ReportSendStatus.Sent, string.Empty, null, JsonString(body, "id"));

        if (status is HttpStatusCode.RequestEntityTooLarge || JsonNumber(body, "code") is TooLargeCode)
            return new ReportSendResult(ReportSendStatus.TooLarge, "The channel says the report is too large.");

        if (status is HttpStatusCode.TooManyRequests)
        {
            var seconds = JsonNumber(body, "retry_after") is { } retry ? retry : response.Headers.RetryAfter?.Delta?.TotalSeconds;

            return new ReportSendResult(ReportSendStatus.RateLimited, "The channel asks to wait.", seconds is { } wait ? TimeSpan.FromSeconds(wait) : null);
        }

        if (status is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ReportSendResult(ReportSendStatus.ChannelGone, $"The channel answered {(int)status}: it no longer takes reports.");

        if ((int)status >= 500)
            return new ReportSendResult(ReportSendStatus.Unavailable, $"The channel answered {(int)status}.");

        return new ReportSendResult(ReportSendStatus.Rejected, $"The channel refused the report ({(int)status}): {ReportWebhook.Hide(JsonString(body, "message") ?? string.Empty)}");
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // A short answer: what a stranger's server sends back is read to a bound.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[16 * 1024];
        var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static string? JsonString(string body, string name)
    {
        try
        {
            using var json = JsonDocument.Parse(body);

            return json.RootElement.ValueKind is JsonValueKind.Object && json.RootElement.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double? JsonNumber(string body, string name)
    {
        try
        {
            using var json = JsonDocument.Parse(body);

            return json.RootElement.ValueKind is JsonValueKind.Object && json.RootElement.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Number ? value.GetDouble() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
