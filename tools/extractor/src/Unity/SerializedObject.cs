namespace D3B.Extractor.Unity;

/// <summary>One object of a bundle: its bytes, the type tree that lays them out, and its file's managed reference types.</summary>
internal sealed class SerializedObject
{
    /// <summary>Gets the object's name, such as <c>map_191106048</c>, or an empty string when it has none.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the type tree of the object's class.</summary>
    public required TypeTree Tree { get; init; }

    /// <summary>Gets the object's bytes.</summary>
    public required byte[] Data { get; init; }

    /// <summary>Gets the type trees of the managed reference classes of the object's file, by full name.</summary>
    public required IReadOnlyDictionary<string, TypeTree> ReferenceTypes { get; init; }
}
