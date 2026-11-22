using Microsoft.Extensions.Logging;

namespace D3B.Bot.Zaaps;

/// <summary>The zaap discovery's log messages, each naming its bot by its character's id.</summary>
internal static partial class ZaapLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: seeks {Count} zaaps it does not know")]
    public static partial void Seeking(ILogger logger, long character, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: goes to the zaap of map {Map}")]
    public static partial void Going(ILogger logger, long character, long map);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: knows the zaap of map {Map}")]
    public static partial void Learnt(ILogger logger, long character, long map);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: gives up the zaap of map {Map}, {Why}")]
    public static partial void GivenUp(ILogger logger, long character, long map, string why);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: discovered {Discovered} of {Sought} zaaps, {OutOfReach} out of reach, {Untaught} not taught on their maps")]
    public static partial void Over(ILogger logger, long character, int discovered, int sought, int outOfReach, int untaught);
}
