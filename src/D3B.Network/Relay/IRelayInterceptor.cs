namespace D3B.Network.Relay;

/// <summary>Watches the frames of each relayed connection, and may rewrite those it asks the relay to hold (D38, D49).</summary>
/// <remarks>
/// <para>
/// The relay forwards each side's bytes as it reads them, as without an interceptor, and reads them as frames besides: a
/// frame is shown once whole, its start kept meanwhile. Only while the connection's interceptor asks for it does the relay
/// hold a frame until it is whole, then forward what the interceptor gives in its place. A frame held for longer than
/// <see cref="RelaySettings.HoldTimeout"/>, or longer than <see cref="RelaySettings.MaxHeldLength"/>, goes on as it came,
/// and the relay holds nothing more on that side: a stream that is no frames cannot hang the connection.
/// </para>
/// <para>
/// A side whose bytes turn out to be no varint frames is watched no more, with a warning, and its bytes still go on; so is
/// a side once its interceptor has thrown, which is logged as an error and never reaches the connection.
/// </para>
/// </remarks>
public interface IRelayInterceptor
{
    /// <summary>Starts watching a connection the relay accepted, before any of its bytes.</summary>
    /// <param name="connectionId">The connection's id, as the relay's log names it.</param>
    /// <param name="route">Which of the client's connections it is.</param>
    /// <returns>What watches the connection's frames, or <see langword="null"/> to relay it unwatched.</returns>
    IConnectionInterceptor? Intercept(string connectionId, RelayRoute route);
}
