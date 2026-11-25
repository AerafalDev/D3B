using Microsoft.Extensions.Logging;

namespace D3B.Bot.Journal;

/// <summary>The log messages of the journals on disk.</summary>
internal static partial class JournalLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Writing the bots' journals and the host's problems in {Directory}")]
    public static partial void Writing(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} journal files past their retention in {Directory}")]
    public static partial void Swept(ILogger logger, int count, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The journals could not be written in {Directory}; the journal in memory goes on")]
    public static partial void WriteFailed(ILogger logger, string directory, Exception exception);
}
