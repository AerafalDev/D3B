namespace D3B.Bot.Reports;

/// <summary>A capture that cannot be cleaned, so that it cannot go in a report (D423).</summary>
public sealed class CaptureScrubException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CaptureScrubException"/> class.</summary>
    public CaptureScrubException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CaptureScrubException"/> class.</summary>
    /// <param name="message">Why the capture cannot be cleaned.</param>
    public CaptureScrubException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CaptureScrubException"/> class.</summary>
    /// <param name="message">Why the capture cannot be cleaned.</param>
    /// <param name="innerException">What failed.</param>
    public CaptureScrubException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
