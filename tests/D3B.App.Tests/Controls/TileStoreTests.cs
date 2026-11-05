using System.Collections.Concurrent;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Tests.Controls;

public sealed class TileStoreTests
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    private static WorldMapTileKey Key(int index, int zoom = 100)
    {
        return new WorldMapTileKey(1, zoom, index);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + _patience;

        while (!condition() && DateTime.UtcNow < until)
            await Task.Delay(5, TestContext.Current.CancellationToken);

        Assert.True(condition(), "The store did not get there in time.");
    }

    [Fact]
    public void TilesWantedAreLoadedOffTheCallingThreadOnceEachAndTheViewIsToldWhenOneArrives()
    {
        var loads = new ConcurrentBag<(WorldMapTileKey Key, int Thread)>();
        var arrived = 0;
        var caller = Environment.CurrentManagedThreadId;
        using var store = new TileStore<string>(key =>
        {
            loads.Add((key, Environment.CurrentManagedThreadId));

            return "tile" + key.Index;
        }, tile => tile.Length, _ => { }, 1_000, 2);

        store.Loaded += () => Interlocked.Increment(ref arrived);
        store.Want([], [Key(1), Key(2), Key(3)]);
        store.Want([], [Key(1), Key(2), Key(3)]);

        // The caller's thread waits where it stands: awaiting would hand it back to the pool, which may then load a tile on it.
        Assert.True(SpinWait.SpinUntil(() => store.Count == 3, TimeSpan.FromSeconds(10)));
        Assert.True(store.TryGet(Key(2), out var tile));
        Assert.Equal("tile2", tile);
        Assert.Equal(3, loads.Count);
        Assert.DoesNotContain(loads, load => load.Thread == caller);
        Assert.Equal(3, Volatile.Read(ref arrived));
    }

    [Fact]
    public async Task WhatTheViewNoLongerWantsIsDroppedFromTheQueueBeforeItLoads()
    {
        var started = new SemaphoreSlim(0);
        var release = new SemaphoreSlim(0);
        var loaded = new ConcurrentQueue<int>();
        using var store = new TileStore<string>(key =>
        {
            loaded.Enqueue(key.Index);
            started.Release();
            release.Wait(TestContext.Current.CancellationToken);

            return "t";
        }, _ => 1, _ => { }, 100, 1);

        store.Want([], [Key(1), Key(2), Key(3)]);
        await started.WaitAsync(TestContext.Current.CancellationToken);
        store.Want([], [Key(3)]);
        release.Release(10);

        await WaitForAsync(() => store.Count == 2);

        // The tile being loaded finishes, the one the view dropped never starts.
        Assert.Equal([1, 3], loaded.ToArray());
        Assert.False(store.TryGet(Key(2), out _));
    }

    [Fact]
    public async Task OverTheBudgetTheLeastRecentlyDrawnGoButNeverATileWantedOrPinned()
    {
        var released = new ConcurrentBag<string>();
        using var store = new TileStore<string>(key => "t" + key.Index, _ => 10, released.Add, 30, 1);

        store.Want([Key(1)], [Key(2), Key(3), Key(4)]);
        await WaitForAsync(() => store.Count == 4);

        // Four tiles of ten in a budget of thirty: all four are wanted, none may go.
        Assert.Empty(released);

        store.Want([Key(1)], [Key(5), Key(6)]);
        await WaitForAsync(() => store.TryGet(Key(5), out _) && store.TryGet(Key(6), out _));
        await WaitForAsync(() => store.Used <= 30);

        Assert.True(store.TryGet(Key(1), out _));
        Assert.Equal(3, store.Count);
        Assert.Equal(["t2", "t3", "t4"], released.Order());
    }

    [Fact]
    public async Task ATileThatFailedIsNotAskedForAgain()
    {
        var attempts = 0;
        using var store = new TileStore<string>(key =>
        {
            Interlocked.Increment(ref attempts);

            throw new KeyNotFoundException();
        }, _ => 1, _ => { }, 10, 1);

        store.Want([], [Key(1)]);
        await WaitForAsync(() => Volatile.Read(ref attempts) == 1);
        await Task.Delay(30, TestContext.Current.CancellationToken);
        store.Want([], [Key(1)]);
        await Task.Delay(30, TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref attempts));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task DisposingLetsGoOfEveryTileKeptAndLoadsNothingMore()
    {
        var released = new ConcurrentBag<string>();
        var store = new TileStore<string>(key => "t" + key.Index, _ => 1, released.Add, 10, 1);

        store.Want([], [Key(1), Key(2)]);
        await WaitForAsync(() => store.Count == 2);
        store.Dispose();
        store.Want([], [Key(3)]);
        await Task.Delay(30, TestContext.Current.CancellationToken);

        Assert.Equal(["t1", "t2"], released.Order());
        Assert.Equal(0, store.Count);
    }
}
