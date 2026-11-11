using System.Text;

namespace D3B.Extractor.Texts;

/// <summary>A text file of the client, <c>Content/I18n/&lt;language&gt;.bin</c>: an Ankama format, not a Unity one.</summary>
/// <remarks>
/// Little-endian throughout (<c>main:docs/client.md</c>, "Texts"): a language code as a string with a 7-bit encoded
/// length, <c>fr</c> in every file whatever its language; an <c>int32</c> count, then that many pairs of an
/// <c>int32</c> text identifier and an <c>int32</c> offset; an <c>int32</c> count, then that many pairs of a 64-bit key
/// hash and an <c>int32</c> offset; then the texts, UTF-8 strings with a 7-bit encoded length, which the offsets point
/// to from the start of the file. The keyed texts belong to the client's interface and are not read.
/// </remarks>
internal sealed class I18nFile
{
    private const int TextEntrySize = sizeof(int) + sizeof(int);
    private const int KeyEntrySize = sizeof(ulong) + sizeof(int);

    private readonly byte[] _data;
    private readonly Dictionary<int, int> _offsets;
    private readonly int _textsStart;

    /// <summary>Gets the number of numbered texts.</summary>
    public int Count => _offsets.Count;

    /// <summary>Gets whether the file holds text 0, which tables name for no text (<c>main:docs/client.md</c>, "Texts").</summary>
    public bool HoldsTextZero => _offsets.ContainsKey(0);

    private I18nFile(byte[] data, Dictionary<int, int> offsets, int textsStart)
    {
        _data = data;
        _offsets = offsets;
        _textsStart = textsStart;
    }

    /// <summary>Reads a text file's tables; the texts are read when asked for.</summary>
    /// <exception cref="InvalidDataException">The file's tables are not laid out as measured, or hold a text twice.</exception>
    public static I18nFile Read(byte[] data)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data, false), Encoding.UTF8);

            reader.ReadString();

            var count = ReadCount(reader, TextEntrySize);
            var offsets = new Dictionary<int, int>(count);

            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();

                if (!offsets.TryAdd(id, reader.ReadInt32()))
                {
                    throw new InvalidDataException($"The texts hold text {id} twice.");
                }
            }

            var keys = ReadCount(reader, KeyEntrySize);

            reader.BaseStream.Position += (long)keys * KeyEntrySize;

            return new I18nFile(data, offsets, checked((int)reader.BaseStream.Position));
        }
        catch (Exception exception) when (exception is IOException or FormatException)
        {
            throw new InvalidDataException($"The texts' tables are not laid out as measured: {exception.Message}", exception);
        }
    }

    /// <summary>Reads a table's count, checked against the bytes left before anything is allocated for it.</summary>
    private static int ReadCount(BinaryReader reader, int entrySize)
    {
        var count = reader.ReadInt32();
        var left = reader.BaseStream.Length - reader.BaseStream.Position;

        if (count < 0 || count > left / entrySize)
        {
            throw new InvalidDataException($"A table of the texts claims {count} entries of {entrySize} bytes, with {left} bytes left.");
        }

        return count;
    }
    /// <summary>Gets the text of an identifier, if the file holds it.</summary>
    /// <exception cref="InvalidDataException">The identifier's offset does not point to a whole text.</exception>
    public bool TryGet(int id, out string text)
    {
        if (!_offsets.TryGetValue(id, out var offset))
        {
            text = "";

            return false;
        }

        if (offset < _textsStart || offset >= _data.Length)
        {
            throw new InvalidDataException($"Text {id} starts at {offset}, outside the texts, which span {_textsStart} to {_data.Length}.");
        }

        try
        {
            using var reader = new BinaryReader(new MemoryStream(_data, offset, _data.Length - offset, false), Encoding.UTF8);

            text = reader.ReadString();

            return true;
        }
        catch (Exception exception) when (exception is IOException or FormatException)
        {
            throw new InvalidDataException($"Text {id} runs past the end of the file, or its length is not one.", exception);
        }
    }

}
