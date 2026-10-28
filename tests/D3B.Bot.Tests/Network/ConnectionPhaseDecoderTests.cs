using System.Buffers;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Network;

public sealed class ConnectionPhaseDecoderTests
{
    private const string Secret = "secret";

    private readonly FakeLogger<ConnectionPhaseDecoder> _log = new();
    private readonly ConnectionPhaseDecoder _decoder;

    public ConnectionPhaseDecoderTests()
    {
        var (schema, mapping) = TestInstall.Protocol();
        var translation = ProtocolTranslation.Create(ProtocolSchema.Load(schema), ProtocolMapping.Parser.ParseFrom(mapping));

        _decoder = new ConnectionPhaseDecoder(translation, _log);
    }

    /// <summary>A connection phase as session 3b captured one, every text holding what must never reach a log.</summary>
    private static IEnumerable<(RelayDirection Direction, ConnectionMessage Message)> ConnectionPhase()
    {
        yield return (RelayDirection.FromClient, new ConnectionMessage
        {
            Request = new ConnectionRequest
            {
                Id = "a",
                Identification = new IdentificationRequest { DeviceId = Secret, Token = new TokenCredentials { Token = Secret }, ClientVersion = Secret },
            },
        });
        yield return (RelayDirection.FromServer, new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "a",
                Identification = new IdentificationResponse { Success = new IdentificationResponse.Types.Success { AccountId = 1, AccountNickname = Secret } },
            },
        });
        yield return (RelayDirection.FromClient, new ConnectionMessage { Request = new ConnectionRequest { Id = "b", ServerSelection = new ServerSelectionRequest { ServerId = 401 } } });
        yield return (RelayDirection.FromServer, new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "b",
                ServerSelection = new ServerSelectionResponse
                {
                    Success = new ServerSelectionResponse.Types.Success { Ticket = Secret, Host = Secret + ".test", Ports = { 5555, 443 } },
                },
            },
        });
    }

    [Fact]
    public void EachFrameIsLoggedByWhatItIsNeverByWhatItHolds()
    {
        var phase = ConnectionPhase().ToList();

        foreach (var (direction, message) in phase)
            _decoder.OnFrame("c1", direction, new ReadOnlySequence<byte>(message.ToByteArray()));

        var lengths = phase.Select(frame => frame.Message.CalculateSize()).ToList();

        Assert.Equal(
            [
                $"Connection c1: the client sent request.identification.token, {lengths[0]} bytes",
                $"Connection c1: the server sent response.identification.success, {lengths[1]} bytes",
                $"Connection c1: the client sent request.server_selection, {lengths[2]} bytes",
                $"Connection c1: the server sent response.server_selection.success, {lengths[3]} bytes",
            ],
            _log.Collector.GetSnapshot().Select(record => record.Message));
        Assert.All(_log.Collector.GetSnapshot(), record => Assert.Equal(LogLevel.Information, record.Level));
        Assert.DoesNotContain(_log.Collector.GetSnapshot(), record => record.Message.Contains(Secret, StringComparison.Ordinal)
            || record.StructuredState!.Any(pair => pair.Value?.Contains(Secret, StringComparison.Ordinal) is true));
    }

    [Fact]
    public void AnEventIsNamedByItsField()
    {
        var message = new ConnectionMessage { Event = new ConnectionEvent { ServerUpdate = new ServerUpdate { Server = new ServerInformation() } } };

        Assert.Equal("event.server_update", ConnectionPhaseDecoder.Describe(message));
    }

    [Fact]
    public void AFrameNotUnderstoodIsAWarning()
    {
        // Field 15 of the root, a varint: no readable message has it.
        byte[] bytes = [.. new ConnectionMessage { Request = new ConnectionRequest { Id = "a", Ping = new Ping() } }.ToByteArray(), 15 << 3, 1];

        _decoder.OnFrame("c1", RelayDirection.FromClient, new ReadOnlySequence<byte>(bytes));

        var record = Assert.Single(_log.Collector.GetSnapshot());

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith($"Connection c1: the client sent request.ping, {bytes.Length} bytes, not understood and relayed as it is: ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesThatAreNoMessageAreAWarning()
    {
        _decoder.OnFrame("c1", RelayDirection.FromServer, new ReadOnlySequence<byte>([0x12, 0x7F, 0x01]));

        var record = Assert.Single(_log.Collector.GetSnapshot());

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith("Connection c1: the server sent no message, 3 bytes, not understood", record.Message, StringComparison.Ordinal);
    }
}
