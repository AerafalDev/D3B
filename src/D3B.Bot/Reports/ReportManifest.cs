namespace D3B.Bot.Reports;

/// <summary>What a report says of itself, in <c>manifest.json</c>: what it is about, which builds it was made with, and every other file with its size and SHA-256 (D410, D411).</summary>
public sealed record ReportManifest
{
    /// <summary>Gets the report's code, <c>D3B-XXXX-XX</c>.</summary>
    public required string Code { get; init; }

    /// <summary>Gets when the report was made.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the app's version.</summary>
    public required string App { get; init; }

    /// <summary>Gets the client build the capture was made with, which the maintainer replays it against.</summary>
    public required string Client { get; init; }

    /// <summary>Gets the pack's version, if known.</summary>
    public string? Pack { get; init; }

    /// <summary>Gets the mapping's version, if known.</summary>
    public string? Mapping { get; init; }

    /// <summary>Gets the area the report is about.</summary>
    public required ReportArea Area { get; init; }

    /// <summary>Gets the character the report is about, by its number, or null for a report of the app.</summary>
    public ReportCharacter? Character { get; init; }

    /// <summary>Gets when the capture started, so that the journal's times join the capture's.</summary>
    public DateTimeOffset? CaptureStartedAt { get; init; }

    /// <summary>Gets whether the player wrote a message of their own (D411).</summary>
    public bool HasMessage { get; init; }

    /// <summary>Gets every other file of the report.</summary>
    public required IReadOnlyList<ReportFile> Files { get; init; }
}
