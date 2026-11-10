namespace D3B.Bot.Reports;

/// <summary>Where a report goes: a channel the maintainers read (D410).</summary>
public interface IReportSink
{
    /// <summary>Gets the most bytes of a file the channel takes.</summary>
    long MaxFileBytes { get; }

    /// <summary>Sends a report.</summary>
    /// <param name="request">The report.</param>
    /// <param name="progress">Told how many bytes of the file went, or null.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>How it ended; a failure is a result, never an exception.</returns>
    Task<ReportSendResult> SendAsync(ReportSendRequest request, IProgress<long>? progress, CancellationToken cancellationToken);
}
