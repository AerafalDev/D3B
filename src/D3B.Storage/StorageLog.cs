using D3B.Storage.Profiles;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>The storage's log messages.</summary>
internal static partial class StorageLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Opened the database {Path}: {Accounts} accounts, {Characters} characters, {Profiles} profiles")]
    public static partial void Opened(ILogger logger, string path, int accounts, int characters, int profiles);

    [LoggerMessage(Level = LogLevel.Error, Message = "What the relay saw of an account could not be recorded")]
    public static partial void RecordFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: started profile {Profile} on its connection")]
    public static partial void StartedOnConnection(ILogger logger, long character, string profile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: profile {Profile} did not start on its connection, {Failure} {Error}")]
    public static partial void NotStartedOnConnection(ILogger logger, long character, string profile, ProfileStartFailure? failure, string? error);
}
