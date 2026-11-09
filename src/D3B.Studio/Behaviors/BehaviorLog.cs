using Microsoft.Extensions.Logging;

namespace D3B.Studio.Behaviors;

/// <summary>What the behaviors log.</summary>
internal static partial class BehaviorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: {Behavior} enters the state {State}")]
    public static partial void StateEntered(ILogger logger, long character, string behavior, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: {Behavior} has no state that holds, and waits")]
    public static partial void NoState(ILogger logger, long character, string behavior);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: {Behavior} cuts its step for the urgent state {State}")]
    public static partial void Urgent(ILogger logger, long character, string behavior, string state);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: the rule {Rule} of {Behavior} failed")]
    public static partial void RuleFailed(ILogger logger, long character, string behavior, string rule, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} assets read in {Folder}, {Problems} files not read")]
    public static partial void AssetsRead(ILogger logger, int count, string folder, int problems);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The file {Path} of the project was not read: {Message}")]
    public static partial void AssetNotRead(ILogger logger, string path, string message);
}
