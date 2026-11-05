using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace D3B.Scripts.Unity;

/// <summary>The addresses of an Addressables group's binary catalog, <c>catalog_1.0.bin</c>, by the key a bundle's container names the asset with.</summary>
/// <remarks>
/// <para>
/// A group's bundle names its assets by a key of 32 hexadecimal characters, and its catalog holds each key's address,
/// such as <c>worldmaps/24/0.75/1.jpg</c>, which the bundle does not (client.md). The catalog's strings are a length as a
/// <c>uint32</c> then ASCII. Before a key's own string stand two offsets that matter: the word 12 bytes before the key's
/// length is the offset of the address's suffix, and the word 8 bytes before it the offset of a record whose first word is
/// the offset of the address's prefix; the address is the prefix, a slash, then the suffix. That is measured, not
/// documented by Unity, so a key whose strings do not read so is left out, and the caller checks that every key it wants
/// came back with an address of the shape it expects.
/// </para>
/// </remarks>
internal static partial class AddressablesCatalog
{
    private const int KeyLength = 32;
    private const int LongestString = 256;

    /// <summary>Reads the addresses of a catalog's keys.</summary>
    /// <param name="path">The catalog's path.</param>
    /// <returns>Each key that reads as such, with its address.</returns>
    /// <exception cref="InvalidDataException">One key has two addresses.</exception>
    public static IReadOnlyDictionary<string, string> AddressesByKey(string path)
    {
        var data = File.ReadAllBytes(path);
        var text = System.Text.Encoding.Latin1.GetString(data);
        var addresses = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in Key().EnumerateMatches(text))
        {
            var at = key.Index;

            if (at < 20 || ReadWord(data, at - 4) != KeyLength)
                continue;

            if (AddressAt(data, at) is not { } address)
                continue;

            var name = text.Substring(at, KeyLength);

            if (!addresses.TryAdd(name, address) && addresses[name] != address)
                throw new InvalidDataException($"{Path.GetFileName(path)} gives key {name} two addresses, {addresses[name]} and {address}.");
        }

        return addresses;
    }

    private static string? AddressAt(byte[] data, int key)
    {
        var suffix = StringAt(data, ReadWord(data, key - 12));
        var node = ReadWord(data, key - 8);

        if (suffix is null || node > data.Length - 4)
            return null;

        var prefix = StringAt(data, ReadWord(data, (int)node));

        return prefix is null ? null : $"{prefix}/{suffix}";
    }

    private static string? StringAt(byte[] data, uint offset)
    {
        if (offset < 4 || offset >= data.Length)
            return null;

        var length = ReadWord(data, (int)offset - 4);

        if (length is 0 or > LongestString || offset + length > data.Length)
            return null;

        var bytes = data.AsSpan((int)offset, (int)length);

        foreach (var value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
                return null;
        }

        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    private static uint ReadWord(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    }

    [GeneratedRegex("[0-9a-f]{32}", RegexOptions.CultureInvariant)]
    private static partial Regex Key();
}
