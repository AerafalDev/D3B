namespace D3B.Extractor.Unity;

/// <summary>One node of a <see cref="TypeTree"/>: a field, its type, how it is laid out, and its children.</summary>
internal sealed class TypeNode
{
    /// <summary>Gets the field's type, as Unity names it, such as <c>int</c>, <c>string</c> or <c>vector</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the field's name as serialized.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the field's name as the source names it: an auto-property's <c>&lt;x&gt;k__BackingField</c> is <c>x</c>.</summary>
    /// <remarks>Some layouts of a class serialize a field, others the backing field of a property of the same name.</remarks>
    public required string Field { get; init; }

    /// <summary>Gets the size of a primitive, in bytes; for other nodes, Unity's sum of their fields, padding excluded.</summary>
    public required int ByteSize { get; init; }

    /// <summary>Gets whether the reader moves to a four-byte boundary after the node.</summary>
    public required bool Align { get; init; }

    /// <summary>Gets whether the node holds a count, then that many items of its second child.</summary>
    public required bool IsArray { get; init; }

    /// <summary>Gets whether the node is a registry of managed references, written once at the end of an object.</summary>
    public required bool IsRegistry { get; init; }

    /// <summary>Gets the indexes of the node's children in the tree.</summary>
    public required int[] Children { get; init; }
}
