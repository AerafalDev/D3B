using System.Buffers;
using System.Net.Sockets;
using D3B.Network.Framing;
using D3B.Network.Relay;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Tests.Relay;

public sealed class RelayObserverTests
{
    private static byte[] Payload(int length, int seed)
    {
        var bytes = new byte[length];

        new Random(seed).NextBytes(bytes);

        return bytes;
    }

    private static byte[] Framed(params byte[][] payloads)
    {
        var writer = new ArrayBufferWriter<byte>();

        foreach (var payload in payloads)
            FrameWriter.Write(writer, payload);

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>A frame as a test compares it: arrays are equal only to themselves, their hexadecimal to any copy.</summary>
    private static (RelayDirection Direction, string Frame) Seen(RelayDirection direction, byte[] frame)
    {
        return (direction, Convert.ToHexString(frame));
    }

    /// <summary>Sends bytes in pieces, each its own write after a pause, so that the relay reads frames cut where they are.</summary>
    private static async Task SendInPiecesAsync(Socket socket, byte[] bytes, params int[] cuts)
    {
        var start = 0;

        foreach (var cut in cuts.Append(bytes.Length))
        {
            await socket.SendAsync(bytes.AsMemory(start, cut - start), TestContext.Current.CancellationToken);
            await Task.Delay(30, TestContext.Current.CancellationToken);
            start = cut;
        }
    }

    [Fact]
    public async Task FramesAreObservedWholeWhereverTheNetworkCutsThem()
    {
        var observer = new RecordingObserver();
        var fromClient = new[] { Payload(10, 1), Payload(300, 2), Payload(5_000, 3) };
        var fromServer = new[] { Payload(455, 4) };
        var clientBytes = Framed(fromClient);
        var serverBytes = Framed(fromServer);

        await using var relay = await TestRelay.StartAsync(observer: observer);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, clientBytes.Length);

            // Cut within the first frame's length, within the second frame, and right after the second.
            await SendInPiecesAsync(client, clientBytes, 1, 100, 313);

            Assert.Equal(clientBytes, await serverReceives);

            var clientReceives = TestRelay.ReadExactlyAsync(client, serverBytes.Length);

            await SendInPiecesAsync(server, serverBytes, 200);

            Assert.Equal(serverBytes, await clientReceives);
        }

        Assert.Equal(
            [.. fromClient.Select(frame => Seen(RelayDirection.FromClient, frame)), .. fromServer.Select(frame => Seen(RelayDirection.FromServer, frame))],
            observer.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task AFrameLargerThanTheTransportsReadBufferIsObservedWhole()
    {
        var observer = new RecordingObserver();

        // Above the 1 MiB of Kestrel's read buffers: the frame comes in many reads, whose bytes are forwarded as they come.
        var payload = Payload(3 * 1024 * 1024, 10);
        var bytes = Framed(payload);

        await using var relay = await TestRelay.StartAsync(observer: observer);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await client.SendAsync(bytes, TestContext.Current.CancellationToken);

            Assert.Equal(bytes, await serverReceives);

            var clientReceives = TestRelay.ReadExactlyAsync(client, bytes.Length);

            await server.SendAsync(bytes, TestContext.Current.CancellationToken);

            Assert.Equal(bytes, await clientReceives);
        }

        Assert.Equal([Seen(RelayDirection.FromClient, payload), Seen(RelayDirection.FromServer, payload)], observer.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task BytesThatAreNoFramesAreRelayedUnobserved()
    {
        var observer = new RecordingObserver();

        // A length of five bytes above 32 bits: no frame can start so.
        byte[] bytes = [0xFF, 0xFF, 0xFF, 0xFF, 0x7F, .. Payload(1_000, 5)];

        await using var relay = await TestRelay.StartAsync(observer: observer);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await SendInPiecesAsync(client, bytes, 3, 500);

            Assert.Equal(bytes, await serverReceives);
        }

        Assert.Empty(observer.Frames);
        await relay.WaitForLogAsync(record => record.Level is LogLevel.Warning && record.Message.Contains("the client's bytes are no varint frames", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFrameCutShortByItsSideClosingIsStillRelayed()
    {
        var observer = new RecordingObserver();
        var whole = Payload(20, 6);
        var cut = Framed(Payload(100, 7))[..41];
        byte[] bytes = [.. Framed(whole), .. cut];

        await using var relay = await TestRelay.StartAsync(observer: observer);
        var (client, server) = await relay.ConnectAsync();

        using (server)
        {
            var serverReceives = TestRelay.ReadToEndAsync(server);

            using (client)
                await SendInPiecesAsync(client, bytes, 30);

            Assert.Equal(bytes, await serverReceives);
        }

        Assert.Equal([Seen(RelayDirection.FromClient, whole)], observer.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task TheStartOfAFrameGoesOnWithoutWaitingForTheRest()
    {
        var observer = new RecordingObserver();

        // As a stream that is no frames would read: a length of 100, then 40 bytes, and the side waits for an answer.
        var cut = Framed(Payload(100, 11))[..41];

        await using var relay = await TestRelay.StartAsync(observer: observer);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(cut, TestContext.Current.CancellationToken);

            Assert.Equal(cut, await TestRelay.ReadExactlyAsync(server, cut.Length));
        }

        Assert.Empty(observer.Frames);
    }

    [Fact]
    public async Task AFailingObserverLeavesTheConnectionRelayed()
    {
        var bytes = Framed(Payload(50, 8), Payload(60, 9));

        await using var relay = await TestRelay.StartAsync(observer: new RecordingObserver { Fails = true });
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await SendInPiecesAsync(client, bytes, 20);

            Assert.Equal(bytes, await serverReceives);
        }

        await relay.WaitForLogAsync(record => record.Level is LogLevel.Error && record.Message.Contains("the client's frames are relayed unobserved", StringComparison.Ordinal));
    }
}
