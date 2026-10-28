using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>The network module's log messages.</summary>
internal static partial class NetworkLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes")]
    public static partial void Decoded(ILogger logger, string connectionId, string side, string message, long length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes, not understood and relayed as it is: {Problem}")]
    public static partial void NotUnderstood(ILogger logger, string connectionId, string side, string message, long length, string problem);
}
