using System.Net;

namespace D3B.Network.Relay;

/// <summary>Finds the real server behind a connection the relay accepted.</summary>
internal interface IUpstreamLocator
{
    /// <summary>Finds the server a client meant to reach when it connected to a listener of the relay.</summary>
    /// <param name="listener">The relay's address and port the client connected to.</param>
    /// <param name="cancellationToken">Stops the search, such as when the client leaves.</param>
    /// <returns>The server.</returns>
    /// <exception cref="UpstreamUnavailableException">No server can be found: the message says why.</exception>
    Task<Upstream> LocateAsync(IPEndPoint listener, CancellationToken cancellationToken);
}
