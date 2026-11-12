namespace D3B.Extractor.Protocol;

/// <summary>A field of a message of the schema, as the wire carries it (docs/protocol.md, "A new build").</summary>
internal sealed class SchemaField
{
    /// <summary>Gets its field number.</summary>
    public required int Number { get; init; }

    /// <summary>Gets its kind: <c>int32</c>, <c>int64</c>, <c>uint32</c>, <c>uint64</c>, <c>bool</c>, <c>string</c>, <c>float</c>, <c>double</c>, <c>bytes</c>, <c>enum</c>, <c>message</c> or <c>map</c>.</summary>
    /// <remarks>
    /// The metadata gives a field's C# type, not its protobuf type: a C# <c>int</c> is written <c>int32</c>, though
    /// <c>sint32</c> and <c>sfixed32</c> are <c>int</c> too, and so for <c>long</c>, <c>uint</c> and <c>ulong</c> with
    /// their <c>sint</c>, <c>fixed</c> and <c>sfixed</c> kinds. On 3.7.12.12 only <c>int32</c>, <c>int64</c> and
    /// <c>uint32</c> were met (inferred, the C# version's D40, read from the machine code); the capture replay's
    /// re-encoding catches another (docs/protocol.md, "Decoding").
    /// </remarks>
    public required string Kind { get; init; }

    /// <summary>Gets the enum or message it holds, a map's values' included, such as <c>hcp</c>; <see langword="null"/> for a scalar.</summary>
    public string? Type { get; init; }

    /// <summary>Gets a map's key kind; <see langword="null"/> for another field.</summary>
    public string? Key { get; init; }

    /// <summary>Gets a map's value kind; <see langword="null"/> for another field.</summary>
    public string? Value { get; init; }

    /// <summary>Gets <c>repeated</c>, or <c>optional</c> for proto3 explicit presence; <see langword="null"/> for neither.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the oneof it belongs to, named after its case enum, such as <c>hcn.hcm</c>; <see langword="null"/> for none.</summary>
    public string? Oneof { get; init; }
}
