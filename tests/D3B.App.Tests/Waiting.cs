using D3B.Data.Tests;

namespace D3B.App.Tests;

/// <summary>Waits for what the app reads off its own thread, as its window does by refreshing.</summary>
internal static class Waiting
{
    public static Task UntilAsync(Func<bool> condition)
    {
        return Polling.UntilAsync(condition);
    }
}
