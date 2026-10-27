using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using Google.Protobuf;

namespace D3B.Protocol.Tests.Mapping;

public sealed class CaptureAnchorsTests
{
    private static readonly Obfuscation _obfuscation = new(ReadableProtocol.Files);
    private static readonly ProtocolSchema _schema = _obfuscation.Schema();

    private static ReadOnlyMemory<byte> Request(string id, Action<ConnectionRequest> content)
    {
        var request = new ConnectionRequest { Id = id };

        content(request);

        return new ConnectionMessage { Request = request }.ToByteArray();
    }

    private static ReadOnlyMemory<byte> Response(string id, Action<ConnectionResponse> content)
    {
        var response = new ConnectionResponse { Id = id };

        content(response);

        return new ConnectionMessage { Response = response }.ToByteArray();
    }

    private static CapturedConnection Connection(bool selects = true)
    {
        var identification = new IdentificationRequest
        {
            DeviceId = "device",
            Token = new TokenCredentials { Token = "token", Shield = new TokenCredentials.Types.Shield { CertificateId = 4, CertificateHash = "hash" } },
            ClientVersion = "1.2.3.4",
        };
        var success = new IdentificationResponse.Types.Success
        {
            AccountId = 5,
            AccountNickname = "nickname",
            Servers = new ServerList { Servers = { new ServerInformation { Server = new GameServer { Id = 6, Type = ServerType.Value2 } } } },
            Rights = new IdentificationResponse.Types.Success.Types.Rights { Flag1 = true },
        };

        return new CapturedConnection(
            [
                Request("1", request => request.Identification = identification),
                Request("2", request =>
                {
                    if (selects)
                        request.ServerSelection = new ServerSelectionRequest { ServerId = 6 };
                    else
                        request.Ping = new Ping();
                }),
            ],
            [
                Response("1", response => response.Identification = new IdentificationResponse { Success = success }),
                Response("2", response =>
                {
                    if (selects)
                        response.ServerSelection = new ServerSelectionResponse { Success = new ServerSelectionResponse.Types.Success { Ticket = "ticket", Host = "host", Ports = { 5555 } } };
                    else
                        response.Pong = new Pong();
                }),
            ]);
    }

    private static AnchorMeasurement Measure(params CapturedConnection[] connections)
    {
        return CaptureAnchors.Measure(connections, _schema.FindFile(ReadableProtocol.ConnectionSchemaFile)!, ReadableProtocol.ConnectionAnchors);
    }

    [Fact]
    public void TheRootAndBothExchangesAreAnchored()
    {
        var measurement = Measure(Connection(), Connection());
        var expected = new[]
        {
            ConnectionMessage.Descriptor,
            ConnectionRequest.Descriptor,
            IdentificationRequest.Descriptor,
            ServerSelectionRequest.Descriptor,
            ConnectionResponse.Descriptor,
            IdentificationResponse.Descriptor,
            ServerSelectionResponse.Descriptor,
        };

        Assert.Empty(measurement.Problems);
        Assert.Equal(8, measurement.Frames);
        Assert.Equal(_obfuscation.Of(ConnectionMessage.Descriptor.FullName), measurement.Root?.FullName);
        Assert.True(measurement.NextTyped < measurement.RootTyped);
        Assert.Equal(expected.Select(message => message.FullName).Order(), measurement.Anchors.Select(anchor => anchor.Readable.FullName).Order());
        Assert.All(measurement.Anchors, anchor => Assert.Equal(_obfuscation.Of(anchor.Readable.FullName), anchor.Obfuscated.FullName));
        Assert.Equal([(1, 1), (2, 2)], measurement.Anchors.Single(anchor => anchor.Readable == ConnectionMessage.Descriptor).Fields);
        Assert.Equal([(3, 3), (4, 4)], measurement.Anchors.Single(anchor => anchor.Readable == ConnectionRequest.Descriptor).Fields);
    }

    [Fact]
    public void AnEventBetweenTheExchangesIsPassedOver()
    {
        var connection = Connection();
        var update = new ConnectionMessage { Event = new ConnectionEvent { ServerUpdate = new ServerUpdate { Server = new ServerInformation() } } }.ToByteArray();
        var measurement = Measure(new CapturedConnection(connection.ClientFrames, [connection.ServerFrames[0], update, connection.ServerFrames[1]]));

        Assert.Empty(measurement.Problems);
        Assert.Equal(7, measurement.Anchors.Count);
        Assert.Equal([(3, 3), (4, 4)], measurement.Anchors.Single(anchor => anchor.Readable == ConnectionResponse.Descriptor).Fields);
    }

    [Fact]
    public void FramesManyMessagesReadAlikeGiveNoRoot()
    {
        var measurement = Measure(new CapturedConnection([new LoginCredentials { Login = "login" }.ToByteArray()], []));

        Assert.Null(measurement.Root);
        Assert.Empty(measurement.Anchors);
        Assert.Contains(measurement.Problems, problem => problem.Contains("none is the root alone", StringComparison.Ordinal));
    }

    [Fact]
    public void NoFrameGivesNoAnchor()
    {
        var measurement = Measure(new CapturedConnection([], []));

        Assert.Null(measurement.Root);
        Assert.Empty(measurement.Anchors);
        Assert.Equal(0, measurement.Frames);
    }

    [Fact]
    public void ConnectionsContradictingEachOtherLeaveTheirAnchorsOut()
    {
        var measurement = Measure(Connection(), Connection(selects: false));

        Assert.NotEmpty(measurement.Problems);
        Assert.DoesNotContain(measurement.Anchors, anchor => anchor.Readable == ServerSelectionRequest.Descriptor || anchor.Readable == ConnectionRequest.Descriptor);
        Assert.Contains(measurement.Anchors, anchor => anchor.Readable == IdentificationRequest.Descriptor);
    }

    [Fact]
    public void AFrameHoldingTwoFieldsAtItsRootIsReported()
    {
        var connection = Connection();
        var both = connection.ClientFrames[0].ToArray().Concat(connection.ServerFrames[0].ToArray()).ToArray();
        var measurement = Measure(new CapturedConnection([both], []));

        Assert.Contains(measurement.Problems, problem => problem.Contains("holds 2 fields", StringComparison.Ordinal));
    }
}
