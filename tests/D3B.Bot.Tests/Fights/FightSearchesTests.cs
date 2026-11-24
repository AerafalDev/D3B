using D3B.Bot.Fights;

namespace D3B.Bot.Tests.Fights;

public sealed class FightSearchesTests
{
    [Fact]
    public async Task ASearchRunsOffTheThreadPool()
    {
        using var searches = new FightSearches(1);

        var onPool = await searches.RunAsync(() => Thread.CurrentThread.IsThreadPoolThread, TestContext.Current.CancellationToken);

        Assert.False(onPool);
    }

    [Fact]
    public async Task ASearchRunsBelowTheRelaysPriority()
    {
        using var searches = new FightSearches(1);

        var priority = await searches.RunAsync(() => Thread.CurrentThread.Priority, TestContext.Current.CancellationToken);

        Assert.Equal(ThreadPriority.BelowNormal, priority);
    }

    [Fact]
    public async Task WhatAwaitsASearchGoesOnOffItsThread()
    {
        using var searches = new FightSearches(1);
        var searching = 0;

        await searches.RunAsync(() => searching = Environment.CurrentManagedThreadId, TestContext.Current.CancellationToken);

        Assert.NotEqual(searching, Environment.CurrentManagedThreadId);
    }

    [Fact]
    public async Task NoMoreSearchesRunAtOnceThanItsThreads()
    {
        using var searches = new FightSearches(2);
        using var gate = new ManualResetEventSlim();
        using var bothRunning = new CountdownEvent(2);
        var started = 0;

        int Search()
        {
            Interlocked.Increment(ref started);
            bothRunning.Signal();
            gate.Wait(TestContext.Current.CancellationToken);

            return 1;
        }

        var first = searches.RunAsync(Search, TestContext.Current.CancellationToken);
        var second = searches.RunAsync(Search, TestContext.Current.CancellationToken);
        var third = searches.RunAsync(() => Interlocked.Increment(ref started), TestContext.Current.CancellationToken);

        bothRunning.Wait(TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref started));
        Assert.False(third.IsCompleted);

        gate.Set();
        await Task.WhenAll(first, second, third);

        Assert.Equal(3, Volatile.Read(ref started));
    }

    [Fact]
    public async Task ASearchThatThrowsFailsItsTaskAndTheThreadsGoOn()
    {
        using var searches = new FightSearches(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => searches.RunAsync<int>(() => throw new InvalidOperationException(), TestContext.Current.CancellationToken));

        Assert.Equal(2, await searches.RunAsync(() => 2, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASearchCancelledBeforeItsTurnNeverRuns()
    {
        using var searches = new FightSearches(1);
        using var gate = new ManualResetEventSlim();
        using var running = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource();
        var ran = false;

        var holding = searches.RunAsync(() => { running.Set(); gate.Wait(TestContext.Current.CancellationToken); return 0; }, TestContext.Current.CancellationToken);

        running.Wait(TestContext.Current.CancellationToken);

        var waiting = searches.RunAsync(() => ran = true, cancel.Token);

        await cancel.CancelAsync();
        gate.Set();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await holding;

        Assert.False(ran);
    }

    [Fact]
    public async Task ASearchAskedOnceDisposedIsRefused()
    {
        var searches = new FightSearches(1);

        searches.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => searches.RunAsync(() => 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ItKeepsAThreadAtLeastAndLeavesCoresToTheRest()
    {
        Assert.InRange(FightSearches.DefaultCount, 1, Math.Max(1, Environment.ProcessorCount / 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FightSearches(0));
    }
}
