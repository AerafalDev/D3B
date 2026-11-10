namespace D3B.Bot.Reports;

/// <summary>What a send came to.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="Detail">What the channel or the network said, short and without the URL, for the player and the log.</param>
/// <param name="RetryAfter">How long the channel asks to wait, when it is rate limited.</param>
/// <param name="MessageId">The id of the post, when it was sent.</param>
public sealed record ReportSendResult(ReportSendStatus Status, string Detail = "", TimeSpan? RetryAfter = null, string? MessageId = null)
{
    /// <summary>Gets whether the report was sent.</summary>
    public bool IsSent => Status is ReportSendStatus.Sent;
}
