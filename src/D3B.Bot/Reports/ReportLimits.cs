namespace D3B.Bot.Reports;

/// <summary>The bounds a reader keeps on a report, which anyone who reads the webhook's URL could post anything to (D410).</summary>
public sealed record ReportLimits
{
    /// <summary>Gets the limits by default: 64 entries, 256 MiB for one, 512 MiB together.</summary>
    public static ReportLimits Default { get; } = new();

    /// <summary>Gets the most entries a zip may hold.</summary>
    public int MaxEntries { get; init; } = 64;

    /// <summary>Gets the most bytes one entry may expand to.</summary>
    public long MaxEntryBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>Gets the most bytes all entries may expand to.</summary>
    public long MaxTotalBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Gets the most bytes the manifest may expand to.</summary>
    public long MaxManifestBytes { get; init; } = 1024 * 1024;
}
