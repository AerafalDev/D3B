using System.IO.Pipelines;

namespace D3B.Network.Captures;

/// <summary>Replays a capture into pipes, in place of the network, for tests and for reading it again (D8, D37).</summary>
public static class CaptureReplay
{
    /// <summary>Writes a capture's records into two pipes, in the capture's order.</summary>
    /// <remarks>
    /// Each record is its own write and flush, so a reader gets the bytes cut where the network cut them, or coarser when
    /// it lags. A side closing completes its pipe, and both are completed at the end. Records are not timed: they follow
    /// one another as fast as the readers take them.
    /// </remarks>
    /// <param name="records">The records.</param>
    /// <param name="fromClient">Receives what the client sent.</param>
    /// <param name="fromServer">Receives what the server sent.</param>
    /// <param name="cancellationToken">Stops the replay.</param>
    /// <returns>A task that completes once every record is written.</returns>
    public static async Task PlayAsync(IEnumerable<CaptureRecord> records, PipeWriter fromClient, PipeWriter fromServer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fromClient);
        ArgumentNullException.ThrowIfNull(fromServer);

        foreach (var record in records)
        {
            switch (record.Kind)
            {
                case CaptureRecordKind.FromClient:
                    await fromClient.WriteAsync(record.Data, cancellationToken);

                    break;
                case CaptureRecordKind.FromServer:
                    await fromServer.WriteAsync(record.Data, cancellationToken);

                    break;
                case CaptureRecordKind.ClientClosed:
                    await fromClient.CompleteAsync();

                    break;
                case CaptureRecordKind.ServerClosed:
                    await fromServer.CompleteAsync();

                    break;
                default:
                    throw new CaptureFormatException($"A record is of kind {record.Kind}, which this format does not have.");
            }
        }

        await fromClient.CompleteAsync();
        await fromServer.CompleteAsync();
    }
}
