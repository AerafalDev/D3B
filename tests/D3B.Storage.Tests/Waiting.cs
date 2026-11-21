using D3B.Data.Tests;

namespace D3B.Storage.Tests;

/// <summary>Waits for what the storage does off the test's thread: a profile started on a bot's connection, a log written.</summary>
internal static class Waiting
{
    public static Task UntilAsync(Func<bool> condition)
    {
        return Polling.UntilAsync(condition);
    }
}
