using System.Net;
using System.Net.Sockets;
using D3B.Network.Clients;

namespace D3B.Network.Relay;

/// <summary>Finds the server through the client's configuration: the connection host Ankama's names, resolved (D35).</summary>
/// <remarks>
/// The host is resolved at each connection, as the client would: it is a load balancer, whose addresses may change. The
/// port is the one the client chose, 5555 or 443, since D3B listens on the same ports as the server.
/// </remarks>
internal sealed class RedirectUpstreamLocator : IUpstreamLocator
{
    private readonly ClientRedirect _redirect;

    public RedirectUpstreamLocator(ClientRedirect redirect)
    {
        _redirect = redirect;
    }

    public async Task<Upstream> LocateAsync(IPEndPoint listener, CancellationToken cancellationToken)
    {
        var host = _redirect.ConnectionHost?.Host ?? throw new UpstreamUnavailableException("The client's configuration does not go to D3B yet.");
        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new UpstreamUnavailableException($"{host} cannot be resolved: {exception.Message}", exception);
        }

        if (addresses.Length is 0)
            throw new UpstreamUnavailableException($"{host} has no address.");

        return new Upstream(host, [.. addresses.Select(address => new IPEndPoint(address, listener.Port))]);
    }
}
