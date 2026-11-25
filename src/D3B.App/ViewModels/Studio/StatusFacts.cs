using D3B.App.Hosting;

namespace D3B.App.ViewModels.Studio;

/// <summary>What the status bar says, as the studio reads it at each refresh (D541).</summary>
internal sealed record StatusFacts
{
    /// <summary>Gets where the host stands, whose relay the bar names.</summary>
    public required HostStatus Host { get; init; }

    /// <summary>Gets why the host did not start, once it failed.</summary>
    public string? Failure { get; init; }

    /// <summary>Gets how many characters the relay carries into the game.</summary>
    public int InGame { get; init; }

    /// <summary>Gets how many bots play an activity now, a fight included.</summary>
    public int Playing { get; init; }

    /// <summary>Gets the client build the host runs for, or the one the app ships.</summary>
    public string ClientBuild { get; init; } = "";

    /// <summary>Gets the format of the game data's pack, once the host opened it.</summary>
    public int? PackFormat { get; init; }

    /// <summary>Gets the version of the extractor that made the pack, once the host opened it.</summary>
    public string? PackExtractor { get; init; }

    /// <summary>Gets how many problems are errors.</summary>
    public int Errors { get; init; }

    /// <summary>Gets how many problems are warnings.</summary>
    public int Warnings { get; init; }

    /// <summary>Gets whether every editor is saved.</summary>
    public bool Saved { get; init; } = true;

    /// <summary>Gets the version of the update downloaded, which a restart installs, or <see langword="null"/>.</summary>
    public string? UpdateVersion { get; init; }
}
