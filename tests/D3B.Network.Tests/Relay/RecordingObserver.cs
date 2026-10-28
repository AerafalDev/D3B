using System.Buffers;
using System.Collections.Concurrent;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

/// <summary>Keeps a copy of every frame the relay shows it, or fails on each when told to.</summary>
internal sealed class RecordingObserver : IRelayObserver
{
    private readonly ConcurrentQueue<(RelayDirection Direction, byte[] Frame)> _frames = new();

    public bool Fails { get; init; }

    public IReadOnlyList<(RelayDirection Direction, byte[] Frame)> Frames => [.. _frames];

    public void OnFrame(string connectionId, RelayDirection direction, ReadOnlySequence<byte> frame)
    {
        if (Fails)
            throw new InvalidOperationException("The observer failed.");

        _frames.Enqueue((direction, frame.ToArray()));
    }
}
