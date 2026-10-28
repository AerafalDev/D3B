using System.Net;
using System.Net.Sockets;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Network.Tests.Relay;

/// <summary>The relay on a port of its own on D3B's address, and a server on 127.0.0.1 that it relays every client to.</summary>
internal sealed class TestRelay : IAsyncDisposable
{
    public const string Host = "connection.test";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly ServiceProvider _services;
    private readonly RelayServer _server;
    private readonly TcpListener _upstream;

    public FakeLogCollector Log { get; }

    public IPEndPoint EndPoint => _server.EndPoints[0];

    private TestRelay(ServiceProvider services, TcpListener upstream, FakeLogCollector log)
    {
        _services = services;
        Log = log;
        _server = services.GetRequiredService<RelayServer>();
        _upstream = upstream;
    }

    /// <summary>Starts the relay, which sends every client to the test's server, or to none when it has none, and shows an observer its frames.</summary>
    public static async Task<TestRelay> StartAsync(bool withServer = true, string? capturesDirectory = null, IRelayObserver? observer = null)
    {
        var upstream = new TcpListener(IPAddress.Loopback, 0);

        upstream.Start();

        var settings = new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = [0],
            ClientDirectory = Path.Combine(AppContext.BaseDirectory, "no-client"),
            RedirectDirectory = Path.Combine(AppContext.BaseDirectory, "no-redirect"),
            CapturesDirectory = capturesDirectory,
            CloseTimeout = TimeSpan.FromSeconds(2),
        };
        var log = new FakeLogCollector();
        var collection = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(log)))
            .AddRelay(settings, _ => "3.7.12.12")
            .AddSingleton<IUpstreamLocator>(new FixedUpstreamLocator(withServer ? new Upstream(Host, [(IPEndPoint)upstream.LocalEndpoint]) : null));

        if (observer is not null)
            collection.AddSingleton(observer);

        var services = collection.BuildServiceProvider();
        var relay = new TestRelay(services, upstream, log);

        await relay._server.StartAsync(TestContext.Current.CancellationToken);

        return relay;
    }

    public static async Task<byte[]> ReadToEndAsync(Socket socket)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8_192];

        while (true)
        {
            var read = await socket.ReceiveAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);

            if (read is 0)
                return bytes.ToArray();

            bytes.Write(buffer, 0, read);
        }
    }

    public static async Task<byte[]> ReadExactlyAsync(Socket socket, int count)
    {
        var bytes = new byte[count];
        var read = 0;

        while (read < count)
        {
            var received = await socket.ReceiveAsync(bytes.AsMemory(read), TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);

            if (received is 0)
                throw new EndOfStreamException($"The connection closed after {read} of {count} bytes.");

            read += received;
        }

        return bytes;
    }

    /// <summary>Connects a client to the relay, and returns it with the server's side of its connection.</summary>
    public async Task<(Socket Client, Socket Server)> ConnectAsync()
    {
        var client = new Socket(SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(EndPoint, TestContext.Current.CancellationToken);

        var server = await _upstream.AcceptSocketAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        return (client, server);
    }

    /// <summary>Waits until the relay has logged a message.</summary>
    public async Task WaitForLogAsync(Func<FakeLogRecord, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(_timeout);

        while (!Log.GetSnapshot().Any(predicate))
            await Task.Delay(20, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync(CancellationToken.None);
        await _services.DisposeAsync();
        _upstream.Dispose();
    }
}
