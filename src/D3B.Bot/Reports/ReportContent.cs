namespace D3B.Bot.Reports;

/// <summary>What a report is made of, before it is written: each part is one the player may uncheck in the dialog (D410).</summary>
public sealed record ReportContent
{
    /// <summary>The most characters of the player's message (D411).</summary>
    public const int MessageLimit = 1500;

    /// <summary>Gets the report's code; see <see cref="ReportCode"/>.</summary>
    public required string Code { get; init; }

    /// <summary>Gets when the report is made.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the app's version.</summary>
    public required string App { get; init; }

    /// <summary>Gets the client build.</summary>
    public required string Client { get; init; }

    /// <summary>Gets the pack's version, if known.</summary>
    public string? Pack { get; init; }

    /// <summary>Gets the mapping's version, if known.</summary>
    public string? Mapping { get; init; }

    /// <summary>Gets what the report is about.</summary>
    public required ReportArea Area { get; init; }

    /// <summary>Gets the character it is about, by number.</summary>
    public ReportCharacter? Character { get; init; }

    /// <summary>Gets the player's own message, or null or empty for none; at most <see cref="MessageLimit"/> characters.</summary>
    public string? Message { get; init; }

    /// <summary>Gets the cleaned capture: required for the areas of the game, absent for a report of the app.</summary>
    public CleanCapture? Capture { get; init; }

    /// <summary>Gets the other files: the journal, the trace, the settings, the assets, what the bots learned.</summary>
    public IReadOnlyList<ReportPart> Parts { get; init; } = [];
}
