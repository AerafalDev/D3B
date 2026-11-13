using System.Text;

namespace D3B.Extractor.Tests.Unity;

/// <summary>Writes an object's bytes by hand, field after field, as a serialized file lays them out.</summary>
internal sealed class ObjectBytes
{
    private readonly List<byte> _bytes = [];

    public void Int32(int value)
    {
        _bytes.AddRange(BitConverter.GetBytes(value));
    }

    public void Int64(long value)
    {
        _bytes.AddRange(BitConverter.GetBytes(value));
    }

    public void Byte(byte value)
    {
        _bytes.Add(value);
    }

    /// <summary>A Unity string: its length, its bytes, then the padding to four bytes.</summary>
    public void String(string value)
    {
        var text = Encoding.UTF8.GetBytes(value);

        Int32(text.Length);
        _bytes.AddRange(text);
        Align();
    }

    public void Raw(IEnumerable<byte> bytes)
    {
        _bytes.AddRange(bytes);
    }

    public void Align()
    {
        while (_bytes.Count % 4 is not 0)
        {
            _bytes.Add(0);
        }
    }

    public byte[] ToArray()
    {
        return [.. _bytes];
    }
}
