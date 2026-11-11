using D3B.Extractor.Unity;

namespace D3B.Extractor.Tests.Unity;

/// <summary>Builds type trees by hand, as a serialized file would store them.</summary>
internal static class Trees
{
    public static FlatNode Node(int level, string type, string name, int byteSize = -1, bool align = false)
    {
        return new FlatNode { Level = level, Type = type, Name = name, ByteSize = byteSize, Align = align };
    }

    public static FlatNode Array(int level, string name = "Array", bool align = false)
    {
        return new FlatNode { Level = level, Type = "Array", Name = name, ByteSize = -1, Align = align, IsArray = true };
    }

    /// <summary>A Unity string: a node holding an aligned array of characters.</summary>
    public static IEnumerable<FlatNode> String(int level, string name)
    {
        yield return Node(level, "string", name);
        yield return Array(level + 1, align: true);
        yield return Node(level + 2, "int", "size", 4);
        yield return Node(level + 2, "char", "data", 1);
    }

    /// <summary>A vector of primitives: a node holding an array of items.</summary>
    public static IEnumerable<FlatNode> Vector(int level, string name, string item, int size)
    {
        yield return Node(level, "vector", name);
        yield return Array(level + 1, align: true);
        yield return Node(level + 2, "int", "size", 4);
        yield return Node(level + 2, item, "data", size);
    }

    public static TypeTree Of(params IEnumerable<FlatNode>[] fields)
    {
        return TypeTree.Build("Test", [Node(0, "Test", "Base"), .. fields.SelectMany(field => field)]);
    }

    public static FlatNode[] One(FlatNode node)
    {
        return [node];
    }

    public static byte[] Bytes(params int[] words)
    {
        return [.. words.SelectMany(BitConverter.GetBytes)];
    }
}
