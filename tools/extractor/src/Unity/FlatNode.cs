namespace D3B.Extractor.Unity;

/// <summary>A node of a type tree as a serialized file stores it: in depth-first order, its depth giving its parent.</summary>
internal readonly record struct FlatNode
{
    /// <summary>Gets the node's depth, 0 for the root.</summary>
    public required int Level { get; init; }

    /// <summary>Gets the field's type, as Unity names it.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the field's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the size of a primitive; for other nodes, Unity's sum of their fields.</summary>
    public required int ByteSize { get; init; }

    /// <summary>Gets whether the reader moves to a four-byte boundary after the node.</summary>
    public bool Align { get; init; }

    /// <summary>Gets whether the node is an array.</summary>
    public bool IsArray { get; init; }

    /// <summary>Gets whether the node is a registry of managed references.</summary>
    public bool IsRegistry { get; init; }
}
