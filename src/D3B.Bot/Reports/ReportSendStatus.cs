namespace D3B.Bot.Reports;

/// <summary>How a send ended.</summary>
public enum ReportSendStatus
{
    /// <summary>The channel took the report.</summary>
    Sent,

    /// <summary>The report is over what the channel takes, and was not sent: the player hands the file over.</summary>
    TooLarge,

    /// <summary>The channel asked to wait before another post.</summary>
    RateLimited,

    /// <summary>The channel no longer exists or its URL was revoked: the app reads the asset again at the next send, which names a new one.</summary>
    ChannelGone,

    /// <summary>The channel refused the report.</summary>
    Rejected,

    /// <summary>The channel could not be reached.</summary>
    Unavailable,
}
