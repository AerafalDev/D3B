namespace D3B.Bot.Reports;

/// <summary>A capture that <see cref="CaptureScrubber"/> cleaned: the only capture a report accepts (D410, D423).</summary>
public sealed class CleanCapture
{
    internal CleanCapture(byte[] bytes, CaptureScrub scrub, DateTimeOffset startedAt)
    {
        Bytes = bytes;
        Scrub = scrub;
        StartedAt = startedAt;
    }

    /// <summary>Gets the cleaned capture's bytes.</summary>
    public byte[] Bytes { get; }

    /// <summary>Gets what was found and overwritten.</summary>
    public CaptureScrub Scrub { get; }

    /// <summary>Gets when the connection started.</summary>
    public DateTimeOffset StartedAt { get; }
}
