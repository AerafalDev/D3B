using Microsoft.Extensions.Logging;

namespace D3B.Bot.Protocol;

/// <summary>The protocol module's log messages.</summary>
internal static partial class ProtocolLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "No {What} for client {Version} at {Path}: downloading {Asset} from release {Tag} of {Repository}, {Authentication}")]
    public static partial void Downloading(ILogger logger, string what, string version, string path, string asset, string tag, string repository, string authentication);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded {Path}, SHA-256 {Hash}")]
    public static partial void Downloaded(ILogger logger, string path, string hash);
}
