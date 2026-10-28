using System.Buffers;

namespace D3B.Network.Relay;

/// <summary>Sees the frames of every relayed connection, as the relay forwards them (D38, D49).</summary>
/// <remarks>
/// <para>
/// The relay forwards each side's bytes as it reads them, as without an observer, and reads them as frames besides: a
/// frame is shown once whole, its start kept meanwhile. A side whose bytes turn out to be no varint frames is observed no
/// more, with a warning, and its bytes still go on.
/// </para>
/// <para>
/// The relay calls it on the connection's own read loop, any number of connections at once: it must be quick, safe from
/// several threads, and must not keep the frame, whose bytes are reused once the call returns. What it throws is logged
/// and ends the observing of that side, never the connection.
/// </para>
/// </remarks>
public interface IRelayObserver
{
    /// <summary>Sees a whole frame, once the relay has forwarded it.</summary>
    /// <param name="connectionId">The connection's id, as the relay's log names it.</param>
    /// <param name="direction">Which side sent it.</param>
    /// <param name="frame">The frame's bytes, its varint length already read.</param>
    void OnFrame(string connectionId, RelayDirection direction, ReadOnlySequence<byte> frame);
}
