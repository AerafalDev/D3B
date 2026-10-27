namespace D3B.Scripts.Capture;

/// <summary>Describes a protobuf message by its shape alone: field numbers, wire types and lengths, never values.</summary>
/// <remarks>
/// A length-delimited field is shown as a nested message when its bytes parse as one, as text when they are printable
/// ASCII, and as bytes otherwise; protobuf's wire format does not say which, so this is a guess, as any reader without the
/// schema makes. The values stay out: a capture holds an account's login ticket.
/// </remarks>
internal static class ProtobufShape
{
    private const int MaxDepth = 12;

    /// <summary>Tells whether bytes parse whole as a protobuf message with at least one field.</summary>
    public static bool IsMessage(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length > 0 && TryFields(bytes, out _);
    }

    /// <summary>Writes the shape of a message, one field a line, nested fields indented.</summary>
    public static void Write(TextWriter output, ReadOnlySpan<byte> bytes, int depth)
    {
        if (!TryFields(bytes, out var fields))
        {
            output.WriteLine($"{new string(' ', 2 * depth)}not a protobuf message ({bytes.Length} bytes)");

            return;
        }

        foreach (var (number, wireType, start, length) in fields)
        {
            var indent = new string(' ', 2 * depth);

            if (wireType is not 2)
            {
                output.WriteLine($"{indent}{number}: {WireName(wireType)}");

                continue;
            }

            var value = bytes.Slice(start, length);

            if (depth < MaxDepth && !IsText(value) && IsMessage(value))
            {
                output.WriteLine($"{indent}{number}: message ({length} bytes)");
                Write(output, value, depth + 1);
            }
            else
            {
                output.WriteLine($"{indent}{number}: {(IsText(value) ? "text" : "bytes")} ({length} bytes)");
            }
        }
    }

    private static bool TryFields(ReadOnlySpan<byte> bytes, out List<(int Number, int WireType, int Start, int Length)> fields)
    {
        fields = [];

        var position = 0;

        while (position < bytes.Length)
        {
            if (!TryVarint(bytes, ref position, out var key) || key >> 3 is 0 or > 536_870_911)
                return false;

            var number = (int)(key >> 3);
            var wireType = (int)(key & 7);
            var start = position;
            int length;

            switch (wireType)
            {
                case 0:
                    if (!TryVarint(bytes, ref position, out _))
                        return false;

                    length = position - start;

                    break;
                case 1:
                    length = 8;

                    break;
                case 2:
                    if (!TryVarint(bytes, ref position, out var declared) || declared > (ulong)(bytes.Length - position))
                        return false;

                    start = position;
                    length = (int)declared;

                    break;
                case 5:
                    length = 4;

                    break;
                default:
                    return false;
            }

            if (start + length > bytes.Length)
                return false;

            position = start + length;
            fields.Add((number, wireType, start, length));
        }

        return true;
    }

    private static bool TryVarint(ReadOnlySpan<byte> bytes, ref int position, out ulong value)
    {
        value = 0;

        for (var shift = 0; shift < 64 && position < bytes.Length; shift += 7)
        {
            var current = bytes[position++];

            value |= (ulong)(current & 0x7F) << shift;

            if (current < 0x80)
                return true;
        }

        return false;
    }

    private static bool IsText(ReadOnlySpan<byte> bytes)
    {
        foreach (var current in bytes)
        {
            if (current is < 0x20 or > 0x7E)
                return false;
        }

        return bytes.Length > 0;
    }

    private static string WireName(int wireType)
    {
        return wireType switch
        {
            0 => "varint",
            1 => "fixed64",
            _ => "fixed32",
        };
    }
}
