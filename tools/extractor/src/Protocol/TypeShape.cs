namespace D3B.Extractor.Protocol;

/// <summary>A type of the client's protocol code as its IL2CPP metadata declares it, no more: what the schema is read from.</summary>
/// <remarks>
/// Cpp2IL gives the metadata (ADR 0010); this shape keeps what the reader needs, so that its rules are tested on shapes
/// written by hand, without a client.
/// </remarks>
internal sealed class TypeShape
{
    /// <summary>Gets the type's full name, a nested type's after its declaring types with dots, such as <c>hdm.hdl.hdg</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether the type is an enum.</summary>
    public required bool IsEnum { get; init; }

    /// <summary>Gets whether the type is a protobuf message: it implements <c>Google.Protobuf.IBufferMessage</c>.</summary>
    public required bool IsMessage { get; init; }

    /// <summary>Gets the values of its <c>const</c> fields in declaration order: an enum's members, or a message's <c>int</c> field numbers.</summary>
    public required IReadOnlyList<long> Literals { get; init; }

    /// <summary>Gets its instance properties in declaration order.</summary>
    public required IReadOnlyList<Property> Properties { get; init; }

    /// <summary>A property: its type and whether it can be set.</summary>
    internal sealed class Property
    {
        public required TypeRef Type { get; init; }

        public required bool HasSetter { get; init; }
    }
}
