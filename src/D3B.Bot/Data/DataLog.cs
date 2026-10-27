using Microsoft.Extensions.Logging;

namespace D3B.Bot.Data;

/// <summary>The data module's log messages.</summary>
internal static partial class DataLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "No pack for client {Version} in {Directory}: downloading {Asset} from release {Tag} of {Repository}, {Authentication}")]
    public static partial void Downloading(ILogger logger, string version, string directory, string asset, string tag, string repository, string authentication);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded {Path}, SHA-256 {Hash}")]
    public static partial void Downloaded(ILogger logger, string path, string hash);
}
