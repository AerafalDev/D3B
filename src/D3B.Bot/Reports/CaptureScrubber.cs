using System.Buffers;
using System.Text;
using D3B.Network.Captures;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Reports;

/// <summary>Writes a copy of a game capture with what names the account overwritten, so that a report can carry it (D410, D421).</summary>
/// <remarks>
/// <para>
/// The ticket and the client's other opaque strings are found by shape: in a frame the client or D3B sent, a string field of 32 characters or
/// more made of digits and <c>a</c> to <c>f</c>. The names of the account's characters are found by value: read through the build's
/// translation from the character list and the choice, then overwritten wherever a string field of either side equals one, and wherever the raw
/// bytes hold one of 6 bytes or more. Fields are renumbered at each build, so no field list would hold; a value does.
/// </para>
/// <para>
/// Each is overwritten by a filler of its own byte length, so that no length, record or frame changes and a replay reads the copy as it read
/// the original. A capture whose character list cannot be read is not cleaned: it is refused, instead of carried with the names in it.
/// </para>
/// </remarks>
public sealed class CaptureScrubber
{
    private const int LeastSecretLength = 32;
    private const int LeastRawNameLength = 6;
    private const int MostNameLength = 256;
    private const int MostDepth = 12;

    private static readonly CaptureRecordKind[] _streams = [CaptureRecordKind.FromClient, CaptureRecordKind.FromServer, CaptureRecordKind.Injected];

    private readonly ProtocolTranslation _translation;

    /// <summary>Initializes a new instance of the <see cref="CaptureScrubber"/> class.</summary>
    /// <param name="translation">The capture's build's translation, which reads the character list and the choice.</param>
    public CaptureScrubber(ProtocolTranslation translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        _translation = translation;
    }

    /// <summary>Cleans a capture into memory, as the only form of capture a report accepts.</summary>
    /// <param name="capture">The capture, which is read and left as it is.</param>
    /// <returns>The cleaned capture.</returns>
    /// <exception cref="CaptureScrubException">The capture holds no character list the translation reads.</exception>
    public CleanCapture Clean(Stream capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var output = new MemoryStream();
        var result = Scrub(capture, output);
        var bytes = output.ToArray();

        using var reader = new CaptureReader(new MemoryStream(bytes));

        return new CleanCapture(bytes, result, reader.Header.StartedAt);
    }

    /// <summary>Copies a capture with the ticket, the opaque strings and the characters' names overwritten.</summary>
    /// <param name="capture">The capture, which is read and left as it is.</param>
    /// <param name="output">Where the copy is written; it is closed.</param>
    /// <returns>What was found and overwritten.</returns>
    /// <exception cref="CaptureScrubException">The capture holds no character list the translation reads.</exception>
    public CaptureScrub Scrub(Stream capture, Stream output)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(output);

        CaptureHeader header;
        List<CaptureRecord> records;

        using (var reader = new CaptureReader(capture))
        {
            header = reader.Header;
            records = [.. reader.ReadAll()];
        }

        var buffers = _streams.ToDictionary(kind => kind, kind => Concatenate(records, kind));
        var frames = buffers.ToDictionary(pair => pair.Key, pair => FrameSpans(pair.Value));
        var names = NamesOf(buffers[CaptureRecordKind.FromServer], frames[CaptureRecordKind.FromServer]);

        if (names.Count is 0)
            throw new CaptureScrubException("The capture holds no character list that the build's translation reads, so there is no name to hide: it cannot go in a report.");

        var nameIndex = names.Select((name, index) => (name, index)).ToDictionary(pair => Latin1(pair.name), pair => pair.index);
        var fields = 0;
        var secrets = new HashSet<string>(StringComparer.Ordinal);
        var replacements = new List<(byte[] Buffer, int Start, byte[] Filler)>();

        foreach (var kind in _streams)
        {
            var buffer = buffers[kind];

            foreach (var (start, length) in frames[kind])
            {
                foreach (var (spanStart, spanLength) in Spans(buffer, start, length))
                {
                    var span = buffer.AsSpan(spanStart, spanLength);

                    if (spanLength <= MostNameLength && nameIndex.TryGetValue(Latin1(span), out var index))
                        replacements.Add((buffer, spanStart, NameFiller(index, spanLength)));
                    else if (kind is not CaptureRecordKind.FromServer && IsSecret(span))
                    {
                        secrets.Add(Latin1(span));
                        replacements.Add((buffer, spanStart, Zeros(spanLength)));
                    }
                }
            }
        }

        foreach (var (buffer, start, filler) in replacements)
            fields += Overwrite(buffer, start, filler);

        foreach (var secret in secrets)
            fields += OverwriteEverywhere(buffers, Encoding.Latin1.GetBytes(secret), Zeros(secret.Length));

        for (var index = 0; index < names.Count; index++)
        {
            if (names[index].Length >= LeastRawNameLength)
                fields += OverwriteEverywhere(buffers, names[index], NameFiller(index, names[index].Length));
        }

        Write(header, records, buffers, output);

        return new CaptureScrub(names.Count, secrets.Count, fields);
    }

    private static byte[] Concatenate(List<CaptureRecord> records, CaptureRecordKind kind)
    {
        var buffer = new byte[records.Where(record => record.Kind == kind).Sum(record => (long)record.Data.Length)];
        var position = 0;

        foreach (var record in records.Where(record => record.Kind == kind))
        {
            record.Data.Span.CopyTo(buffer.AsSpan(position));
            position += record.Data.Length;
        }

        return buffer;
    }

    // The frames of a stream: a varint length, then that many bytes. A last frame cut short is read to the end.
    private static List<(int Start, int Length)> FrameSpans(byte[] buffer)
    {
        var frames = new List<(int, int)>();
        var position = 0;

        while (position < buffer.Length)
        {
            var length = 0L;
            var shift = 0;
            var size = 0;

            while (position + size < buffer.Length && size < 5)
            {
                var next = buffer[position + size++];

                length |= (long)(next & 0x7f) << shift;
                shift += 7;

                if (next < 0x80)
                    break;
            }

            var start = position + size;
            var end = Math.Min(buffer.Length, start + length);

            frames.Add((start, (int)(end - start)));
            position = (int)end;
        }

        return frames;
    }

    private List<byte[]> NamesOf(byte[] server, List<(int Start, int Length)> frames)
    {
        var names = new List<byte[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void See(string? name)
        {
            if (!string.IsNullOrEmpty(name) && seen.Add(name))
                names.Add(Encoding.UTF8.GetBytes(name));
        }

        foreach (var (start, length) in frames)
        {
            var decoded = _translation.DecodeGame(new ReadOnlySequence<byte>(server.AsMemory(start, length)));

            switch (decoded.Content)
            {
                case CharacterListEvent list:
                    foreach (var character in list.Characters)
                        See(character.Basics?.Name);

                    break;

                case CharacterSelectionEvent { Success.Character: { } chosen }:
                    See(chosen.Basics?.Name);

                    break;
            }
        }

        return names;
    }

    // Every length-delimited field of a message, at every depth: a name or a secret is one of them, whatever its message.
    private static List<(int Start, int Length)> Spans(byte[] buffer, int start, int length)
    {
        var spans = new List<(int, int)>();

        Collect(buffer, start, length, 0, spans);

        return spans;
    }

    private static void Collect(byte[] buffer, int start, int length, int depth, List<(int, int)> spans)
    {
        if (depth > MostDepth || !TryParse(buffer, start, length, out var fields))
            return;

        foreach (var (fieldStart, fieldLength) in fields)
        {
            spans.Add((fieldStart, fieldLength));

            if (fieldLength > 1)
                Collect(buffer, fieldStart, fieldLength, depth + 1, spans);
        }
    }

    // Reads a whole message, or says it is none: a string often is not one, and then its bytes are not walked.
    private static bool TryParse(byte[] buffer, int start, int length, out List<(int Start, int Length)> fields)
    {
        fields = [];

        var position = start;
        var end = start + length;

        while (position < end)
        {
            if (!TryVarint(buffer, ref position, end, out var tag) || (tag >> 3) is 0)
                return false;

            switch ((int)(tag & 7))
            {
                case 0:
                    if (!TryVarint(buffer, ref position, end, out _))
                        return false;

                    break;

                case 1:
                    position += 8;

                    break;

                case 2:
                    if (!TryVarint(buffer, ref position, end, out var size) || size > (ulong)(end - position))
                        return false;

                    fields.Add((position, (int)size));
                    position += (int)size;

                    break;

                case 5:
                    position += 4;

                    break;

                default:
                    return false;
            }

            if (position > end)
                return false;
        }

        return fields.Count > 0 || length is 0;
    }

    private static bool TryVarint(byte[] buffer, ref int position, int end, out ulong value)
    {
        value = 0;

        for (var shift = 0; shift < 70 && position < end; shift += 7)
        {
            var next = buffer[position++];

            value |= (ulong)(next & 0x7f) << shift;

            if (next < 0x80)
                return true;
        }

        return false;
    }

    private static bool IsSecret(ReadOnlySpan<byte> span)
    {
        if (span.Length < LeastSecretLength)
            return false;

        foreach (var value in span)
        {
            if (!(value is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f'))
                return false;
        }

        return true;
    }

    private static string Latin1(ReadOnlySpan<byte> bytes)
    {
        return Encoding.Latin1.GetString(bytes);
    }

    private static string Latin1(byte[] bytes)
    {
        return Encoding.Latin1.GetString(bytes);
    }

    // Distinct for each name, so that the list's characters stay told apart; ASCII, of the name's own byte length.
    private static byte[] NameFiller(int index, int length)
    {
        var text = "Player" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Encoding.ASCII.GetBytes(text.Length >= length ? text[..length] : text.PadRight(length, 'x'));
    }

    private static byte[] Zeros(int length)
    {
        return Enumerable.Repeat((byte)'0', length).ToArray();
    }

    private static int Overwrite(byte[] buffer, int start, byte[] filler)
    {
        var target = buffer.AsSpan(start, filler.Length);

        if (target.SequenceEqual(filler))
            return 0;

        filler.CopyTo(target);

        return 1;
    }

    private static int OverwriteEverywhere(Dictionary<CaptureRecordKind, byte[]> buffers, byte[] value, byte[] filler)
    {
        var overwritten = 0;

        foreach (var buffer in buffers.Values)
        {
            var from = 0;

            while (from <= buffer.Length - value.Length && buffer.AsSpan(from).IndexOf(value) is var found and >= 0)
            {
                overwritten += Overwrite(buffer, from + found, filler);
                from += found + value.Length;
            }
        }

        return overwritten;
    }

    private static void Write(CaptureHeader header, List<CaptureRecord> records, Dictionary<CaptureRecordKind, byte[]> buffers, Stream output)
    {
        var positions = _streams.ToDictionary(kind => kind, _ => 0);

        using var writer = new CaptureWriter(output, header, TimeProvider.System);

        foreach (var record in records)
        {
            if (!buffers.TryGetValue(record.Kind, out var buffer))
            {
                writer.Copy(record);

                continue;
            }

            var position = positions[record.Kind];

            writer.Copy(record with { Data = buffer.AsMemory(position, record.Data.Length) });
            positions[record.Kind] = position + record.Data.Length;
        }
    }
}
