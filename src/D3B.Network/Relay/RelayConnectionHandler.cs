using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using D3B.Network.Captures;
using D3B.Network.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>Relays each client's connection to the real server, bytes as they are, both ways (D36).</summary>
/// <remarks>
/// <para>
/// Kestrel calls it once per accepted connection, from any number of clients at once; it keeps no state between them.
/// The server's connection is a <see cref="ConnectionContext"/> too, made by Kestrel's socket layer (D8), so both sides
/// are pipes.
/// </para>
/// <para>
/// Each direction forwards what it reads as soon as it reads it, and records it when captures are on. With an
/// <see cref="IRelayObserver"/>, it also reads the bytes as frames, each shown to the observer once whole (D49). When a
/// side closes, the other's output is completed, which closes it once its last bytes are sent. A side still open after
/// <see cref="RelaySettings.CloseTimeout"/> is cut: the relay keeps no half-open connection.
/// </para>
/// </remarks>
internal sealed class RelayConnectionHandler : ConnectionHandler
{
    private readonly RelaySettings _settings;
    private readonly IUpstreamLocator _locator;
    private readonly SocketConnectionContextFactory _connections;
    private readonly TimeProvider _time;
    private readonly Func<string?> _clientVersion;
    private readonly IRelayObserver? _observer;
    private readonly ILogger _log;

    public RelayConnectionHandler(
        RelaySettings settings,
        IUpstreamLocator locator,
        SocketConnectionContextFactory connections,
        TimeProvider time,
        Func<string?> clientVersion,
        IRelayObserver? observer,
        ILogger<RelayConnectionHandler> log)
    {
        _settings = settings;
        _locator = locator;
        _connections = connections;
        _time = time;
        _clientVersion = clientVersion;
        _observer = observer;
        _log = log;
    }

    private static string SideOf(RelayDirection direction)
    {
        return direction is RelayDirection.FromClient ? "client" : "server";
    }

    public override async Task OnConnectedAsync(ConnectionContext connection)
    {
        var started = _time.GetTimestamp();
        var listener = (IPEndPoint)connection.LocalEndPoint!;
        Upstream upstream;
        ConnectionContext? server;
        IPEndPoint? endPoint;

        // The client leaving, or the host stopping, ends the search for its server: neither waits for a timeout.
        var stopRequested = connection.Features.Get<IConnectionLifetimeNotificationFeature>()?.ConnectionClosedRequested ?? CancellationToken.None;

        using (var reaching = CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionClosed, stopRequested))
        {
            try
            {
                upstream = await _locator.LocateAsync(listener, reaching.Token);
                (server, endPoint) = await ConnectAsync(connection.ConnectionId, upstream, reaching.Token);
            }
            catch (UpstreamUnavailableException exception)
            {
                RelayLog.NoUpstream(_log, connection.ConnectionId, connection.RemoteEndPoint, exception.Message);

                return;
            }
            catch (OperationCanceledException) when (reaching.IsCancellationRequested)
            {
                // There is nothing to relay.
                return;
            }
        }

        if (server is null)
        {
            RelayLog.NoUpstream(_log, connection.ConnectionId, connection.RemoteEndPoint, $"No address of {upstream.Host} answered.");

            return;
        }

        await using (server)
        {
            RelayLog.Relaying(_log, connection.ConnectionId, connection.RemoteEndPoint, upstream.Host, endPoint!);

            using var capture = StartCapture(connection, upstream, endPoint!);

            // The host stopping asks every connection to close: both sides are cut, as when the process ends.
            using var stopping = stopRequested.Register(() =>
            {
                connection.Abort();
                server.Abort();
            });

            var fromClient = PumpAsync(connection.ConnectionId, connection.Transport.Input, server.Transport.Output, capture, RelayDirection.FromClient);
            var fromServer = PumpAsync(connection.ConnectionId, server.Transport.Input, connection.Transport.Output, capture, RelayDirection.FromServer);
            var first = await Task.WhenAny(fromClient, fromServer);
            var second = first == fromClient ? fromServer : fromClient;

            if (await Task.WhenAny(second, Task.Delay(_settings.CloseTimeout, _time)) != second)
            {
                connection.Abort();
                server.Abort();
            }

            var bytesFromClient = await fromClient;
            var bytesFromServer = await fromServer;
            var duration = _time.GetElapsedTime(started);

            RelayLog.Closed(_log, connection.ConnectionId, duration, bytesFromClient, bytesFromServer);

            if (capture?.Failure is { } failure)
                RelayLog.CaptureFailed(_log, connection.ConnectionId, failure.Message);
        }
    }

    private async Task<long> PumpAsync(string connectionId, PipeReader input, PipeWriter output, CaptureWriter? capture, RelayDirection direction)
    {
        var kind = direction is RelayDirection.FromClient ? CaptureRecordKind.FromClient : CaptureRecordKind.FromServer;
        var bytes = 0L;

        // The start of a frame not yet whole, already forwarded, kept to show the observer the frame once the rest comes.
        var pending = _observer is null ? null : new ArrayBufferWriter<byte>();

        try
        {
            while (true)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    capture?.Write(kind, buffer);

                    foreach (var segment in buffer)
                        output.Write(segment.Span);

                    bytes += buffer.Length;

                    if (pending is not null && !Observe(connectionId, direction, pending, buffer))
                        pending = null;
                }

                input.AdvanceTo(buffer.End);

                if (result.IsCompleted || result.IsCanceled)
                    break;

                // Completed when the other side is gone: nothing more can reach it.
                if ((await output.FlushAsync()).IsCompleted)
                    break;
            }

            await output.FlushAsync();
        }
        catch (Exception exception) when (exception is IOException or ConnectionAbortedException or ConnectionResetException or SocketException or ObjectDisposedException)
        {
            // A connection reset or cut ends its direction like a close: the relay has nothing to answer it with.
        }
        finally
        {
            capture?.WriteClosed(kind is CaptureRecordKind.FromClient ? CaptureRecordKind.ClientClosed : CaptureRecordKind.ServerClosed);

            await input.CompleteAsync();
            await output.CompleteAsync();
        }

        return bytes;
    }

    /// <summary>Shows the observer each whole frame of what a side has sent, the start of a frame kept for the next read.</summary>
    /// <param name="connectionId">The connection's id.</param>
    /// <param name="direction">The side that sent the bytes.</param>
    /// <param name="pending">The start of a frame earlier reads left, which keeps the start this read leaves.</param>
    /// <param name="read">The bytes this read brought, already forwarded.</param>
    /// <returns>Whether to go on observing the side: not once its bytes are no frames, or the observer has failed.</returns>
    private bool Observe(string connectionId, RelayDirection direction, ArrayBufferWriter<byte> pending, ReadOnlySequence<byte> read)
    {
        var bytes = read;

        if (pending.WrittenCount > 0)
        {
            foreach (var segment in read)
                pending.Write(segment.Span);

            bytes = new ReadOnlySequence<byte>(pending.WrittenMemory);
        }

        var reader = new SequenceReader<byte>(bytes);

        try
        {
            while (FrameReader.TryReadFrame(ref reader, out var frame))
                _observer!.OnFrame(connectionId, direction, frame);
        }
        catch (FrameFormatException exception)
        {
            RelayLog.Unframed(_log, connectionId, SideOf(direction), exception.Message);

            return false;
        }
        catch (Exception exception)
        {
            // The observer only watches: its failure must not cut the player off.
            RelayLog.ObserverFailed(_log, connectionId, SideOf(direction), exception);

            return false;
        }

        // Copied out first: the rest may lie in the very buffer it is written back to.
        var rest = bytes.Slice(reader.Position).ToArray();

        pending.ResetWrittenCount();
        pending.Write(rest);

        return true;
    }

    private async Task<(ConnectionContext? Server, IPEndPoint? EndPoint)> ConnectAsync(string connectionId, Upstream upstream, CancellationToken cancellationToken)
    {
        foreach (var endPoint in upstream.EndPoints)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(_settings.ConnectTimeout);
                await socket.ConnectAsync(endPoint, timeout.Token);

                return (_connections.Create(socket), endPoint);
            }
            catch (Exception exception) when (exception is SocketException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                socket.Dispose();
                RelayLog.ConnectFailed(_log, connectionId, endPoint, upstream.Host, exception is SocketException ? exception.Message : "It timed out.");
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        }

        return (null, null);
    }

    private CaptureWriter? StartCapture(ConnectionContext connection, Upstream upstream, IPEndPoint server)
    {
        if (_settings.CapturesDirectory is not { } directory)
            return null;

        var startedAt = _time.GetUtcNow();
        var header = new CaptureHeader
        {
            StartedAt = startedAt,
            ClientVersion = _clientVersion() ?? string.Empty,
            Listener = connection.LocalEndPoint?.ToString() ?? string.Empty,
            ServerHost = upstream.Host,
            Server = server.ToString(),
        };

        try
        {
            var (writer, path) = CaptureWriter.Create(directory, $"{startedAt:yyyyMMdd-HHmmss}-{connection.ConnectionId}", header, _time);

            RelayLog.Capturing(_log, connection.ConnectionId, path);

            return writer;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A capture is for reading the session again: losing it is no reason to cut the player off.
            RelayLog.CaptureFailed(_log, connection.ConnectionId, exception.Message);

            return null;
        }
    }
}
