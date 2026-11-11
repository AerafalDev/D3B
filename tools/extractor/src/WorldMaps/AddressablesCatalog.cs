using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace D3B.Extractor.WorldMaps;

/// <summary>The addresses of an Addressables group's binary catalog, <c>catalog_1.0.bin</c>, by the key a bundle names an asset with.</summary>
/// <remarks>
/// The world maps' bundle names its tiles by keys of 32 hexadecimal characters; only its catalog holds each key's address,
/// such as <c>worldmaps/24/0.75/1.jpg</c> (3.7.13.17, <c>main:docs/client.md</c>, "The world's maps"). Its strings are a
/// length as a <c>uint32</c> then ASCII. Before a key's string, the word 12 bytes before its length is the offset of the
/// address's suffix, and the word 8 bytes before it the offset of a record whose first word is the offset of the
/// address's prefix. That is measured, not documented by Unity: a key whose strings do not read so is left out, and the
/// caller checks that each key it wants came back with an address of the shape it expects.
/// </remarks>
internal static partial class AddressablesCatalog
{
    private const int KeyLength = 32;
    private const int LongestString = 256;

    /// <summary>Reads the addresses of a catalog's keys.</summary>
    /// <exception cref="InvalidDataException">One key has two addresses.</exception>
    public static IReadOnlyDictionary<string, string> AddressesByKey(byte[] data)
    {
        var text = Encoding.Latin1.GetString(data);
        var addresses = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in Key().EnumerateMatches(text))
        {
            var at = key.Index;

            if (at < 20 || Word(data, at - 4) != KeyLength || AddressAt(data, at) is not { } address)
            {
                continue;
            }

            var name = text.Substring(at, KeyLength);

            if (!addresses.TryAdd(name, address) && addresses[name] != address)
            {
                throw new InvalidDataException($"The catalog gives key {name} two addresses, {addresses[name]} and {address}.");
            }
        }

        return addresses;
    }

    private static string? AddressAt(byte[] data, int key)
    {
        var suffix = StringAt(data, Word(data, key - 12));
        var record = Word(data, key - 8);

        if (suffix is null || record > data.Length - 4)
        {
            return null;
        }

        var prefix = StringAt(data, Word(data, (int)record));

        return prefix is null ? null : $"{prefix}/{suffix}";
    }

    private static string? StringAt(byte[] data, uint offset)
    {
        if (offset < 4 || offset >= data.Length)
        {
            return null;
        }

        var length = Word(data, (int)offset - 4);

        if (length is 0 or > LongestString || offset + length > data.Length)
        {
            return null;
        }

        var bytes = data.AsSpan((int)offset, (int)length);

        foreach (var value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
            {
                return null;
            }
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static uint Word(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    }

    [GeneratedRegex("[0-9a-f]{32}", RegexOptions.CultureInvariant)]
    private static partial Regex Key();
}
