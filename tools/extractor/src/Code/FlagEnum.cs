namespace D3B.Extractor.Code;

/// <summary>A flags enum of the client's code, such as <c>MapInformationFlags</c>: the name of each of its bits.</summary>
/// <remarks>
/// The data folder writes a record's flags as the names of the bits it sets, in camelCase (docs/data.md): a bit the
/// client's enum does not name fails the extraction, rather than being written as a number no one can read.
/// </remarks>
internal sealed class FlagEnum
{
    private readonly (ulong Bit, string Name)[] _bits;

    /// <summary>Gets the enum's full name, for errors.</summary>
    public string Name { get; }

    private FlagEnum(string name, (ulong Bit, string Name)[] bits)
    {
        Name = name;
        _bits = bits;
    }

    /// <summary>Builds the enum from its members: each one bit, or zero for the member that names none.</summary>
    /// <exception cref="InvalidDataException">A member sets several bits, or two members the same one.</exception>
    public static FlagEnum From(string name, IEnumerable<(string Name, ulong Value)> members)
    {
        var bits = new List<(ulong Bit, string Name)>();

        foreach (var (member, value) in members)
        {
            if (value is 0)
            {
                continue;
            }

            // A member of several bits is a combination of others, which a name per bit already says.
            if (!ulong.IsPow2(value))
            {
                continue;
            }

            if (bits.Any(bit => bit.Bit == value))
            {
                throw new InvalidDataException($"{name} names bit {ulong.Log2(value)} twice.");
            }

            bits.Add((value, CamelCase(member)));
        }

        return new FlagEnum(name, [.. bits.OrderBy(bit => bit.Bit)]);
    }

    /// <summary>The names of the bits <paramref name="value"/> sets, in the order of the bits.</summary>
    /// <exception cref="InvalidDataException">The value sets a bit the enum does not name.</exception>
    public IReadOnlyList<string> Names(ulong value)
    {
        var names = new List<string>();
        var left = value;

        foreach (var (bit, name) in _bits)
        {
            if ((value & bit) is not 0)
            {
                names.Add(name);
                left &= ~bit;
            }
        }

        if (left is not 0)
        {
            throw new InvalidDataException($"A record's flags set bit {ulong.Log2(left & (~left + 1))}, which {Name} does not name.");
        }

        return names;
    }

    private static string CamelCase(string name)
    {
        return name.Length is 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
