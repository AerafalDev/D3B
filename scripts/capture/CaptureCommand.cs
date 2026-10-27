using System.Buffers;
using System.CommandLine;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Network.Relay;

namespace D3B.Scripts.Capture;

/// <summary>The command that describes captures by their structure: reads, frames, sizes and shapes, never content.</summary>
internal static class CaptureCommand
{
    public static RootCommand Create()
    {
        var paths = new Argument<string[]>("paths")
        {
            Description = @"Capture files, or directories of them. Defaults to %LOCALAPPDATA%\D3B\captures, where the host records them.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var frames = new Option<bool>("--frames")
        {
            Description = "Also shows the shape of every frame: protobuf field numbers, wire types and lengths, never values.",
        };
        var command = new RootCommand("Describes D3B's captures of relayed connections by their structure, never their content (D37).")
        {
            paths,
            frames,
        };

        command.SetAction(result => Run(result.GetValue(paths) ?? [], result.GetValue(frames)));

        return command;
    }

    private static int Run(string[] paths, bool frames)
    {
        var files = (paths.Length is 0 ? [RelaySettings.DefaultCapturesDirectory] : paths)
            .SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*" + CaptureWriter.FileExtension).Order(StringComparer.Ordinal).ToArray() : [path])
            .ToList();

        if (files.Count is 0)
        {
            Console.Error.WriteLine("No capture to describe. The host records them with --Network:Captures:Enabled true.");

            return 2;
        }

        var exitCode = 0;

        foreach (var file in files)
        {
            try
            {
                Describe(Console.Out, file, frames);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{file}: {exception.Message}");
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static void Describe(TextWriter output, string path, bool frames)
    {
        using var reader = CaptureReader.Open(path);

        var records = reader.ReadAll().ToList();
        var header = reader.Header;
        var last = records.Count > 0 ? records[^1].Offset : TimeSpan.Zero;

        output.WriteLine($"{Path.GetFileName(path)}: client {(header.ClientVersion.Length > 0 ? header.ClientVersion : "unknown")}, "
            + $"listener port {Port(header.Listener)}, server port {Port(header.Server)}, {records.Count} records over {last:hh\\:mm\\:ss\\.fff}{(reader.IsTruncated ? ", cut short" : string.Empty)}");
        output.WriteLine($"  order: {string.Join(", ", records.Take(16).Select(Name))}{(records.Count > 16 ? ", ..." : string.Empty)}");

        foreach (var (kind, side) in new[] { (CaptureRecordKind.FromClient, "client"), (CaptureRecordKind.FromServer, "server") })
            DescribeSide(output, records.Where(record => record.Kind == kind).ToList(), side, frames);
    }

    private static void DescribeSide(TextWriter output, List<CaptureRecord> reads, string side, bool frames)
    {
        var bytes = reads.SelectMany(read => read.Data.ToArray()).ToArray();

        output.Write($"  from the {side}: {reads.Count} reads, {bytes.Length} bytes");

        if (bytes.Length is 0)
        {
            output.WriteLine();

            return;
        }

        // A TLS connection opens with a handshake record: type 22, then version 3.x.
        output.Write(bytes.Length > 2 && bytes[0] is 0x16 && bytes[1] is 0x03 ? ", TLS" : ", not TLS");

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var sizes = new List<int>();
        var ends = new HashSet<long>();
        using var shapes = new StringWriter();

        try
        {
            while (FrameReader.TryReadFrame(ref reader, out var frame))
            {
                sizes.Add((int)frame.Length);
                ends.Add(reader.Consumed);

                if (frames)
                {
                    shapes.WriteLine($"    frame {sizes.Count}, {frame.Length} bytes:");
                    ProtobufShape.Write(shapes, frame.ToArray(), 3);
                }
            }
        }
        catch (FrameFormatException exception)
        {
            output.WriteLine($"; no varint frame at byte {reader.Consumed}: {exception.Message}");

            return;
        }

        var offset = 0L;
        var aligned = reads.Count(read => ends.Contains(offset += read.Data.Length));
        var messages = sizes.Count is 0 ? 0 : Frames(bytes).Count(frame => ProtobufShape.IsMessage(frame));

        output.WriteLine($"; {sizes.Count} varint frames{(sizes.Count > 0 ? $" of {sizes.Min()} to {sizes.Max()} bytes" : string.Empty)}, "
            + $"{bytes.Length - reader.Consumed} bytes left over, {messages} parse as protobuf, {aligned} of {reads.Count} reads end on a frame");
        output.Write(shapes.ToString());
    }

    private static List<byte[]> Frames(byte[] bytes)
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var frames = new List<byte[]>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        return frames;
    }

    private static string Name(CaptureRecord record)
    {
        return record.Kind switch
        {
            CaptureRecordKind.FromClient => $"client {record.Data.Length}",
            CaptureRecordKind.FromServer => $"server {record.Data.Length}",
            CaptureRecordKind.ClientClosed => "client closed",
            _ => "server closed",
        };
    }

    private static string Port(string endPoint)
    {
        var separator = endPoint.LastIndexOf(':');

        return separator < 0 ? "unknown" : endPoint[(separator + 1)..];
    }
}
