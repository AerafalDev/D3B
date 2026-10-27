using System.Buffers;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Mapping;

namespace D3B.Scripts.Mapping;

/// <summary>Reads the frames of the captures the host recorded of a build's connections (D37, D38).</summary>
internal static class CaptureFrames
{
    /// <summary>Reads the captures of a build, both sides of each, framed.</summary>
    /// <param name="paths">Capture files, or directories of them.</param>
    /// <param name="clientVersion">The build whose captures to read; the others are skipped.</param>
    /// <param name="skipped">The number of captures of other builds, or that could not be read.</param>
    public static List<CapturedConnection> Read(IEnumerable<string> paths, string clientVersion, out int skipped)
    {
        var connections = new List<CapturedConnection>();

        skipped = 0;

        foreach (var file in paths.SelectMany(Files))
        {
            try
            {
                using var reader = CaptureReader.Open(file);

                var records = reader.ReadAll().ToList();

                if (reader.Header.ClientVersion != clientVersion)
                {
                    skipped++;

                    continue;
                }

                connections.Add(new CapturedConnection(Frames(records, CaptureRecordKind.FromClient), Frames(records, CaptureRecordKind.FromServer)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FrameFormatException)
            {
                skipped++;
            }
        }

        return connections;
    }

    private static IEnumerable<string> Files(string path)
    {
        if (Directory.Exists(path))
            return Directory.GetFiles(path, "*" + CaptureWriter.FileExtension).Order(StringComparer.Ordinal);

        return File.Exists(path) ? [path] : [];
    }

    private static List<ReadOnlyMemory<byte>> Frames(List<CaptureRecord> records, CaptureRecordKind kind)
    {
        var bytes = records.Where(record => record.Kind == kind).SelectMany(record => record.Data.ToArray()).ToArray();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var frames = new List<ReadOnlyMemory<byte>>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        return frames;
    }
}
