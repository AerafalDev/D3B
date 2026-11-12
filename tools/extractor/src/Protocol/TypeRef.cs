namespace D3B.Extractor.Protocol;

/// <summary>A type a property names: its full name, such as <c>System.Int32</c> or <c>Google.Protobuf.Collections.RepeatedField`1</c>, and a generic type's arguments.</summary>
internal sealed class TypeRef
{
    public required string Name { get; init; }

    public IReadOnlyList<TypeRef> Arguments { get; init; } = [];

    public override string ToString()
    {
        return Arguments.Count is 0 ? Name : $"{Name}<{string.Join(", ", Arguments)}>";
    }
}
