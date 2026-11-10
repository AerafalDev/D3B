using System.Text.RegularExpressions;

namespace D3B.Bot.Reports;

/// <summary>The URL of the private Discord webhook reports go to (D410): a secret, never in the repository, which the app reads from a release asset.</summary>
/// <remarks>
/// Only a Discord webhook URL is accepted, so that an asset replaced by something else can send reports nowhere but to a Discord channel. The URL is
/// never printed: <see cref="ToString"/> hides its token.
/// </remarks>
public sealed partial class ReportWebhook
{
    private ReportWebhook(Uri url)
    {
        Url = url;
    }

    /// <summary>Gets the URL, which holds the token that lets anyone post: it goes to the request and nowhere else.</summary>
    public Uri Url { get; }

    /// <summary>Reads a webhook URL.</summary>
    /// <param name="text">The text of the asset, which may hold spaces and a line break around the URL.</param>
    /// <param name="webhook">The webhook.</param>
    /// <returns>Whether the text is the URL of a Discord webhook: https, discord.com or discordapp.com, <c>/api/webhooks/{id}/{token}</c> and nothing else.</returns>
    public static bool TryParse(string? text, out ReportWebhook? webhook)
    {
        webhook = null;

        if (text is null || !Pattern().IsMatch(text.Trim()))
            return false;

        webhook = new ReportWebhook(new Uri(text.Trim()));

        return true;
    }

    /// <summary>Reads a webhook URL that must be one.</summary>
    /// <param name="text">The URL.</param>
    /// <returns>The webhook.</returns>
    /// <exception cref="FormatException">The text is no Discord webhook URL.</exception>
    public static ReportWebhook Parse(string text)
    {
        return TryParse(text, out var webhook) ? webhook! : throw new FormatException("The text is no Discord webhook URL.");
    }

    /// <summary>Removes any webhook URL from a text, so that a message about a failure can be shown and logged.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with every <c>/api/webhooks/...</c> replaced.</returns>
    public static string Hide(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Leak().Replace(text, "/api/webhooks/[hidden]");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Url.Host}/api/webhooks/[hidden]";
    }

    [GeneratedRegex(@"^https://(?:discord|discordapp)\.com/api/webhooks/[0-9]{17,20}/[A-Za-z0-9_-]{8,100}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex(@"/api/webhooks/\S*", RegexOptions.CultureInvariant)]
    private static partial Regex Leak();
}
