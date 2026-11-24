using D3B.Bot.Journal;
using D3B.Bot.Reports;
using D3B.Bot.Runtime;
using D3B.Protocol.Translation;

namespace D3B.App.ViewModels.Reports;

/// <summary>Everything a report of one character can be made of, read from the running host: the dialog only asks for it once a character is chosen (D410).</summary>
internal sealed record ReportSources
{
    /// <summary>Gets the character's id on the game server.</summary>
    public required long CharacterId { get; init; }

    /// <summary>Gets the character's name, which no text of the report holds.</summary>
    public required string CharacterName { get; init; }

    /// <summary>Gets the names of the account's other characters, which no text of the report holds either.</summary>
    public IReadOnlyList<string> OtherNames { get; init; } = [];

    /// <summary>Gets the character's level, if known.</summary>
    public int? Level { get; init; }

    /// <summary>Gets the character's breed, if known.</summary>
    public int? Breed { get; init; }

    /// <summary>Gets the capture of the character's last game connection, or null when there is none.</summary>
    public string? CapturePath { get; init; }

    /// <summary>Gets the translation of the capture's build, which the cleaner reads the character list with, or null when the build has none.</summary>
    public ProtocolTranslation? Translation { get; init; }

    /// <summary>Gets the character's journal in memory, oldest first, which the report holds when the disk has none.</summary>
    public IReadOnlyList<JournalEntry> Journal { get; init; } = [];

    /// <summary>Gets what reads the character's journal of the last day from the journals on disk, null or empty when they hold none, run off the UI's thread as the report is prepared (D504).</summary>
    public Func<string?>? JournalOnDisk { get; init; }

    /// <summary>Gets what reads the host's problems of the last day from the journals on disk, null or empty when they hold none, run off the UI's thread as the report is prepared (D504).</summary>
    public Func<string?>? HostJournal { get; init; }

    /// <summary>Gets the player's own folder, which no text of the report holds, or null.</summary>
    public string? UserFolder { get; init; }

    /// <summary>Gets the character's decisions, oldest first.</summary>
    public IReadOnlyList<TraceEntry> Trace { get; init; } = [];

    /// <summary>Gets the files of the profile the character plays and of what it reaches.</summary>
    public IReadOnlyList<ReportPart> Assets { get; init; } = [];

    /// <summary>Gets what the bots learned, read from the database when the report is prepared, or null when there is no database.</summary>
    public Func<CancellationToken, Task<ReportPart>>? Knowledge { get; init; }

    /// <summary>Gets the settings the player changed, by their configuration's keys.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    /// <summary>Gets the app's version.</summary>
    public required string App { get; init; }

    /// <summary>Gets the client build.</summary>
    public required string Client { get; init; }

    /// <summary>Gets the pack's version, if known.</summary>
    public string? Pack { get; init; }

    /// <summary>Gets the mapping's version, if known.</summary>
    public string? Mapping { get; init; }

    /// <summary>Gets the folder a report is written in.</summary>
    public required string Folder { get; init; }

    /// <summary>Gets the clock the report is dated by.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
