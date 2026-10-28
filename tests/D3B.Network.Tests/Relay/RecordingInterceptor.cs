using System.Buffers;
using System.Collections.Concurrent;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

/// <summary>Watches every connection, keeping a copy of every frame and rewriting those it holds; or fails on each frame when told to.</summary>
internal sealed class RecordingInterceptor : IRelayInterceptor, IConnectionInterceptor
{
    private readonly ConcurrentQueue<(RelayDirection Direction, byte[] Frame, bool Held)> _frames = new();
    private readonly ConcurrentQueue<string> _abandoned = new();

    public bool Fails { get; init; }

    /// <summary>Gets the side whose frames are held, or null when none is.</summary>
    public RelayDirection? Holding { get; set; }

    /// <summary>Gets what a frame held is rewritten to, from what it was, or null to forward it as it came.</summary>
    public Func<byte[], byte[]?>? Rewrite { get; init; }

    public IReadOnlyList<(RelayDirection Direction, byte[] Frame)> Frames => [.. _frames.Select(frame => (frame.Direction, frame.Frame))];

    public IReadOnlyList<bool> Held => [.. _frames.Select(frame => frame.Held)];

    public IReadOnlyList<string> Abandoned => [.. _abandoned];

    public IConnectionInterceptor? Intercept(string connectionId, RelayRoute route)
    {
        return this;
    }

    public bool Holds(RelayDirection direction)
    {
        return Holding == direction;
    }

    public byte[]? OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held)
    {
        if (Fails)
            throw new InvalidOperationException("The interceptor failed.");

        var bytes = frame.ToArray();

        _frames.Enqueue((direction, bytes, held));

        return held ? Rewrite?.Invoke(bytes) : null;
    }

    public void OnHoldAbandoned(RelayDirection direction, string reason)
    {
        _abandoned.Enqueue(reason);
    }
}
