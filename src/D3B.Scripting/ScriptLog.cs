using Microsoft.Extensions.Logging;

namespace D3B.Scripting;

/// <summary>The scripts' log messages, each naming its bot by its character's id.</summary>
internal static partial class ScriptLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: script {Script} says {Text}")]
    public static partial void Said(ILogger logger, long character, string script, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: script {Script} tells an error, {Text}")]
    public static partial void Erred(ILogger logger, long character, string script, string text);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: script {Script} failed, {Error}")]
    public static partial void Failed(ILogger logger, long character, string script, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {Count} scripts, {Broken} of which do not read, in {Directory}")]
    public static partial void Listed(ILogger logger, int count, int broken, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Script {Script} does not read: {Error}")]
    public static partial void ScriptBroken(ILogger logger, string script, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scripts' folder {Directory} could not be made or read: no script is listed")]
    public static partial void FolderFailed(ILogger logger, string directory, Exception exception);
}
