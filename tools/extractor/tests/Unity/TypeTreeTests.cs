using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Unity;

public sealed class TypeTreeTests
{
    [Fact]
    public void EachNodeHoldsTheNodesOneLevelBelowIt()
    {
        var tree = Of(One(Node(1, "int", "a", 4)), String(1, "b"), One(Node(1, "float", "c", 4)));

        Assert.Equal([1, 2, 6], tree[0].Children);
        Assert.Equal([3], tree[2].Children);
        Assert.Equal(6, tree.Child(0, "c"));
        Assert.Equal(-1, tree.Child(0, "missing"));
    }

    [Fact]
    public void ABackingFieldIsFoundByItsPropertysName()
    {
        var tree = Of(One(Node(1, "int", "<Id>k__BackingField", 4)));

        Assert.Equal(1, tree.Child(0, "Id"));
        Assert.Equal("Id", tree[1].Field);
    }

    [Fact]
    public void AMissingFieldThatIsRequiredFails()
    {
        var tree = Of(One(Node(1, "int", "a", 4)));

        var error = Assert.Throws<InvalidDataException>(() => tree.Required(0, "b"));

        Assert.Contains("Test.Base has no field b", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATreeThatSkipsALevelIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => TypeTree.Build("Test", [Node(0, "Test", "Base"), Node(2, "int", "a", 4)]));
    }

    [Fact]
    public void ATreeWithoutRootIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => TypeTree.Build("Test", [Node(1, "int", "a", 4)]));
        Assert.Throws<InvalidDataException>(() => TypeTree.Build("Test", []));
    }

    [Fact]
    public void AnArrayWithoutSizeAndItemIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => TypeTree.Build("Test", [Node(0, "Test", "Base"), Array(1), Node(2, "int", "size", 4)]));
    }

    [Fact]
    public void AFieldThatHoldsNoArrayIsNotReadAsOne()
    {
        var tree = Of(One(Node(1, "int", "a", 4)));

        Assert.Throws<InvalidDataException>(() => tree.ArrayOf(1));
    }
}
