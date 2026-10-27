using Microsoft.Extensions.Logging;

namespace D3B.Host;

/// <summary>The host's log messages.</summary>
internal static partial class HostLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Built {Count} modules: {Modules}")]
    public static partial void ModulesBuilt(ILogger logger, int count, string modules);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} plugins from {Directory}")]
    public static partial void PluginsLoaded(ILogger logger, int count, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {Name} {Version} [{Id}] by {Author}")]
    public static partial void PluginLoaded(ILogger logger, string name, string version, string id, string author);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened the pack of client {Version}, format {Format}: {Path}")]
    public static partial void PackOpened(ILogger logger, string version, int format, string path);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The host does not start. {Reason}")]
    public static partial void DataUnavailable(ILogger logger, string reason);
}
