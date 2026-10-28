using D3B.Bot.Movement;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>The bots' log messages, each naming its bot by its character's id.</summary>
internal static partial class BotLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plays through game connection {ConnectionId}")]
    public static partial void Attached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: game connection {ConnectionId} closed")]
    public static partial void Detached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the player sent {Request}, the bot pauses until it is resumed")]
    public static partial void PausedByPlayer(ILogger logger, long character, string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: resumed")]
    public static partial void Resumed(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its behavior was interrupted, {Reason}")]
    public static partial void Interrupted(ILogger logger, long character, Interruption reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: a message could not be handled")]
    public static partial void HandlerFailed(ILogger logger, long character, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game connection {ConnectionId}: its account is not known, so no bot plays its character")]
    public static partial void AccountUnknown(ILogger logger, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks from cell {From} to cell {To}, {Steps} steps")]
    public static partial void Walking(ILogger logger, long character, int from, int to, int steps);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walk to cell {To} over, {Outcome}")]
    public static partial void Walked(ILogger logger, long character, int to, WalkOutcome outcome);
}
