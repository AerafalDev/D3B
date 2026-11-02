using System.Globalization;
using System.Text;

namespace D3B.Scripts.Icons;

/// <summary>Writes SVG's outlines in Avalonia's path syntax: a path's data, a rectangle and a circle.</summary>
/// <remarks>
/// SVG lets numbers run together, such as <c>1.5.5</c> or an arc's flags written <c>011</c>, which Avalonia's parser does
/// not always read: the data is read token by token and written again with every number apart. Avalonia fills by the
/// even-odd rule unless told, SVG by the non-zero one: the rule is always written first.
/// </remarks>
internal static class SvgPath
{
    /// <summary>Writes a path's data again.</summary>
    /// <param name="data">The path's <c>d</c>.</param>
    /// <param name="evenOdd">Whether it fills by the even-odd rule.</param>
    /// <returns>The data in Avalonia's syntax.</returns>
    public static string FromData(string data, bool evenOdd)
    {
        ArgumentNullException.ThrowIfNull(data);

        var output = new StringBuilder(evenOdd ? "F0" : "F1");
        var position = 0;
        var command = '\0';
        var index = 0;

        while (true)
        {
            SkipSeparators(data, ref position);

            if (position >= data.Length)
                break;

            if (char.IsAsciiLetter(data[position]))
            {
                command = data[position++];
                index = 0;
                output.Append(' ').Append(command);

                if (command is 'Z' or 'z')
                    continue;
            }
            else if (command is '\0' or 'Z' or 'z')
            {
                throw new InvalidDataException($"The path {data} has a number where a command is due, at {position}.");
            }

            // An arc's fourth and fifth values are flags, one digit each, which SVG lets touch what follows.
            var isFlag = command is 'A' or 'a' && index % 7 is 3 or 4;
            var number = isFlag ? ReadFlag(data, ref position) : ReadNumber(data, ref position);

            output.Append(' ').Append(number);
            index++;
        }

        return output.ToString();
    }

    /// <summary>Writes a rectangle as a path, its corners rounded when it says so.</summary>
    public static string FromRectangle(double x, double y, double width, double height, double rx, double ry)
    {
        if (rx <= 0 && ry <= 0)
            return Invariant($"F1 M {x} {y} H {x + width} V {y + height} H {x} Z");

        rx = Math.Min(rx > 0 ? rx : ry, width / 2);
        ry = Math.Min(ry > 0 ? ry : rx, height / 2);

        return Invariant(
            $"F1 M {x + rx} {y} H {x + width - rx} A {rx} {ry} 0 0 1 {x + width} {y + ry} V {y + height - ry} A {rx} {ry} 0 0 1 {x + width - rx} {y + height} H {x + rx} A {rx} {ry} 0 0 1 {x} {y + height - ry} V {y + ry} A {rx} {ry} 0 0 1 {x + rx} {y} Z");
    }

    /// <summary>Writes an ellipse as a path, two half arcs.</summary>
    public static string FromEllipse(double cx, double cy, double rx, double ry)
    {
        return Invariant($"F1 M {cx - rx} {cy} A {rx} {ry} 0 1 1 {cx + rx} {cy} A {rx} {ry} 0 1 1 {cx - rx} {cy} Z");
    }

    private static string Invariant(FormattableString text)
    {
        return text.ToString(CultureInfo.InvariantCulture);
    }

    private static void SkipSeparators(string data, ref int position)
    {
        while (position < data.Length && (char.IsWhiteSpace(data[position]) || data[position] == ','))
            position++;
    }

    private static string ReadFlag(string data, ref int position)
    {
        if (data[position] is not ('0' or '1'))
            throw new InvalidDataException($"The path {data} has an arc's flag that is neither 0 nor 1, at {position}.");

        return data[position++].ToString();
    }

    private static string ReadNumber(string data, ref int position)
    {
        var start = position;

        if (position < data.Length && data[position] is '+' or '-')
            position++;

        var seenDot = false;
        var digits = 0;

        while (position < data.Length && (char.IsAsciiDigit(data[position]) || (data[position] == '.' && !seenDot)))
        {
            seenDot |= data[position] == '.';
            digits += char.IsAsciiDigit(data[position]) ? 1 : 0;
            position++;
        }

        if (position < data.Length && data[position] is 'e' or 'E')
        {
            position++;

            if (position < data.Length && data[position] is '+' or '-')
                position++;

            while (position < data.Length && char.IsAsciiDigit(data[position]))
                position++;
        }

        if (digits == 0)
            throw new InvalidDataException($"The path {data} has something that is not a number, at {start}.");

        return double.Parse(data.AsSpan(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
    }
}
