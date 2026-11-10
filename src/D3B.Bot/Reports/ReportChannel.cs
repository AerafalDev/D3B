namespace D3B.Bot.Reports;

/// <summary>Where the app finds the channel reports go to: the URL of a private Discord webhook, which is a secret and so is not in the repository but in a release asset (D410).</summary>
/// <remarks>
/// <para>
/// The asset is read at the moment of a send, not stored: revoking the webhook and publishing another URL changes where reports go without a new app,
/// at the price that anyone who downloads the app can read it, which D410 says. An environment variable names another URL for a test or a fork.
/// A text that is no Discord webhook URL is no channel: the player is told, and hands the file over.
/// </para>
/// </remarks>
public sealed class ReportChannel
{
    /// <summary>The name of the environment variable that names the webhook instead of the asset.</summary>
    public const string EnvironmentVariable = "D3B_REPORT_WEBHOOK";

    /// <summary>The tag of the release that holds the asset.</summary>
    public const string ReleaseTag = "report-channel";

    /// <summary>The name of the asset: a text holding the webhook's URL.</summary>
    public const string AssetName = "webhook.txt";

    private const int MostAssetBytes = 512;

    private readonly HttpClient _http;
    private readonly Uri _asset;
    private readonly string? _override;
    private readonly long _maxFileBytes;
    private ReportWebhook? _webhook;

    /// <summary>Initializes a new instance of the <see cref="ReportChannel"/> class.</summary>
    /// <param name="http">The client that reads the asset and posts.</param>
    /// <param name="asset">Where the asset is; see <see cref="AssetOf"/>.</param>
    /// <param name="webhookOverride">A webhook URL that stands for the asset, or null.</param>
    /// <param name="maxFileBytes">The most bytes of a report the channel takes.</param>
    public ReportChannel(HttpClient http, Uri asset, string? webhookOverride = null, long maxFileBytes = DiscordReportSink.DefaultMaxFileBytes)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(asset);

        _http = http;
        _asset = asset;
        _override = webhookOverride;
        _maxFileBytes = maxFileBytes;
    }

    /// <summary>Gets where the asset of a repository's release is.</summary>
    /// <param name="repository">The repository, as owner/name.</param>
    /// <returns>The asset's URL.</returns>
    public static Uri AssetOf(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        return new Uri($"https://github.com/{repository}/releases/download/{ReleaseTag}/{AssetName}");
    }

    /// <summary>Finds the channel.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The sink, or null when no channel can be found: the asset is missing, unreachable, or holds no Discord webhook URL.</returns>
    public async Task<IReportSink?> GetSinkAsync(CancellationToken cancellationToken)
    {
        if (_webhook is null)
        {
            _webhook = _override is not null
                ? (ReportWebhook.TryParse(_override, out var named) ? named : null)
                : await ReadAssetAsync(cancellationToken).ConfigureAwait(false);
        }

        return _webhook is null ? null : new DiscordReportSink(_http, _webhook, _maxFileBytes);
    }

    private async Task<ReportWebhook?> ReadAssetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(_asset, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MostAssetBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[MostAssetBytes + 1];
            var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken).ConfigureAwait(false);

            return read > MostAssetBytes || !ReportWebhook.TryParse(System.Text.Encoding.UTF8.GetString(buffer, 0, read), out var webhook) ? null : webhook;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
