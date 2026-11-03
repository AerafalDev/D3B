using System.Buffers;
using D3B.Bot.Network;
using D3B.Bot.Tests.Network;
using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Google.Protobuf;

namespace D3B.Bot.Tests.Runtime;

/// <summary>A game connection a test drives: what each side sends, shown to its game session as the relay shows it, and the requests D3B sent.</summary>
internal sealed class TestConnection : IDisposable
{
    private readonly FakeRelayConnection _connection;
    private readonly IConnectionInterceptor _interceptor;
    private readonly ProtocolTranslation _translation;
    private readonly List<IMessage> _withheld = [];

    public GameSession Session { get; }

    /// <summary>Gets the client's requests its game session withheld from the server (D367).</summary>
    public IReadOnlyList<IMessage> Withheld => [.. _withheld];

    /// <summary>Gets the requests D3B sent the server, readable.</summary>
    public IReadOnlyList<IMessage> Sent => [.. _connection.Sent.Select(frame => _translation.DecodeGame(new ReadOnlySequence<byte>(frame)).Content!)];

    public TestConnection(FakeRelayConnection connection, IConnectionInterceptor interceptor, ProtocolTranslation translation)
    {
        _connection = connection;
        _interceptor = interceptor;
        _translation = translation;
        Session = (GameSession)interceptor;
    }

    public void Client(IMessage request)
    {
        if (Show(RelayDirection.FromClient, TestGame.Request(request)) is { Length: 0 })
            _withheld.Add(request);
    }

    public void Server(IMessage content)
    {
        Show(RelayDirection.FromServer, TestGame.Event(content));
    }

    public void Response(IMessage content)
    {
        Show(RelayDirection.FromServer, TestGame.Response(content));
    }

    /// <summary>Waits until D3B sent a number of requests, and gives the last.</summary>
    public async Task<IMessage> SentAsync(int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (_connection.Sent.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"D3B sent {_connection.Sent.Count} requests, not {count}.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return Sent[count - 1];
    }

    public void Close()
    {
        _connection.Close();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private byte[]? Show(RelayDirection direction, byte[] frame)
    {
        // As the relay does: a frame the session asks to hold may be rewritten, to nothing for one withheld.
        return _interceptor.OnFrame(direction, new ReadOnlySequence<byte>(frame), _interceptor.Holds(direction));
    }
}
