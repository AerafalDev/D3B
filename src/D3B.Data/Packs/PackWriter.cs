using System.Buffers;
using System.Buffers.Binary;
using MemoryPack;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace D3B.Data.Packs;

/// <summary>Writes a pack: entries one after another, then the index once <see cref="Complete"/> is called.</summary>
/// <remarks>
/// The same entries, written in the same order at the same level, give the same bytes: zstd runs on one thread and
/// MemoryPack writes members in declaration order. A pack can therefore be compared with an earlier extraction by its
/// hash.
/// </remarks>
public sealed class PackWriter : IDisposable
{
    /// <summary>The zstd level used when none is given: packs are written once per build and read many times.</summary>
    public const int DefaultCompressionLevel = 19;

    private readonly Stream _output;
    private readonly PackInfo _info;
    private readonly Compressor _compressor;
    private readonly ArrayBufferWriter<byte> _serialized = new();
    private readonly List<PackEntry> _entries = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private long _position;
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="PackWriter"/> class, and writes the pack's header.</summary>
    /// <param name="output">Where to write the pack. The writer does not dispose it.</param>
    /// <param name="info">What the pack is made from.</param>
    /// <param name="compressionLevel">The zstd level of every entry.</param>
    public PackWriter(Stream output, PackInfo info, int compressionLevel = DefaultCompressionLevel)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(info);

        _output = output;
        _info = info;
        _compressor = new Compressor(compressionLevel);
        _compressor.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);

        Span<byte> header = stackalloc byte[PackLayout.HeaderLength];

        PackLayout.Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[PackLayout.Magic.Length..], GamePack.FormatVersion);
        Write(header);
    }

    /// <summary>Adds an entry.</summary>
    /// <typeparam name="T">The type the entry is written as, and read back as.</typeparam>
    /// <param name="name">The entry's name, unique in the pack, such as <c>texts/fr</c>.</param>
    /// <param name="value">The entry.</param>
    /// <exception cref="ArgumentException">The pack already holds an entry of that name.</exception>
    /// <exception cref="InvalidOperationException">The pack is complete.</exception>
    public void Add<T>(string name, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_completed)
            throw new InvalidOperationException("The pack is complete: no entry can be added.");

        if (!_names.Add(name))
            throw new ArgumentException($"The pack already holds an entry named '{name}'.", nameof(name));

        var (offset, compressedLength, length) = WriteCompressed(value);

        _entries.Add(new PackEntry
        {
            Name = name,
            Type = typeof(T).FullName!,
            Offset = offset,
            CompressedLength = compressedLength,
            Length = length,
        });
    }

    /// <summary>Writes the index and the trailer. A pack is not readable before.</summary>
    /// <exception cref="InvalidOperationException">The pack is already complete.</exception>
    public void Complete()
    {
        if (_completed)
            throw new InvalidOperationException("The pack is already complete.");

        _completed = true;

        var (offset, compressedLength, length) = WriteCompressed(new PackIndex { Info = _info, Entries = [.. _entries] });

        Span<byte> trailer = stackalloc byte[PackLayout.TrailerLength];

        BinaryPrimitives.WriteInt64LittleEndian(trailer, offset);
        BinaryPrimitives.WriteInt32LittleEndian(trailer[8..], compressedLength);
        BinaryPrimitives.WriteInt32LittleEndian(trailer[12..], length);
        PackLayout.Magic.CopyTo(trailer[16..]);
        Write(trailer);
        _output.Flush();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _compressor.Dispose();
    }

    private (long Offset, int CompressedLength, int Length) WriteCompressed<T>(T value)
    {
        _serialized.ResetWrittenCount();
        MemoryPackSerializer.Serialize(_serialized, value);

        var source = _serialized.WrittenSpan;
        var buffer = ArrayPool<byte>.Shared.Rent(Compressor.GetCompressBound(source.Length));

        try
        {
            var compressedLength = _compressor.Wrap(source, buffer);
            var offset = _position;

            Write(buffer.AsSpan(0, compressedLength));

            return (offset, compressedLength, source.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void Write(ReadOnlySpan<byte> bytes)
    {
        _output.Write(bytes);
        _position += bytes.Length;
    }
}
