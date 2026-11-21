namespace D3B.Data.Tests;

/// <summary>How long a test waits for what runs off its thread, one deadline for every test project.</summary>
/// <remarks>
/// A condition that holds holds within milliseconds; the deadline only bounds one that never will. Ten seconds were
/// seen running out on a machine loaded by other test hosts and replays (2026-11-20), so the margin is wide.
/// </remarks>
internal static class Polling
{
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Waits until a condition holds, failing the test once the deadline passed.</summary>
    /// <param name="condition">What is waited for.</param>
    /// <param name="failure">The test's failure once the deadline passed.</param>
    public static async Task UntilAsync(Func<bool> condition, string failure = "The condition did not hold in time.")
    {
        var deadline = DateTime.UtcNow + Deadline;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, failure);
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
