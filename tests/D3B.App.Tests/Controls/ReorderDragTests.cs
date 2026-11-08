using D3B.App.Controls;

namespace D3B.App.Tests.Controls;

public sealed class ReorderDragTests
{
    [Theory]
    [InlineData(0, 2, false, 1)]
    [InlineData(0, 2, true, 2)]
    [InlineData(3, 1, false, 1)]
    [InlineData(3, 1, true, 2)]
    [InlineData(1, 1, false, 1)]
    [InlineData(1, 1, true, 1)]
    [InlineData(1, 0, true, 1)]
    [InlineData(1, 2, false, 1)]
    public void ARowDroppedBeforeOrAfterAnotherTakesThePlaceItLeavesOnceItLeftItsOwn(int from, int index, bool after, int expected)
    {
        Assert.Equal(expected, ReorderDrag.Place(from, index, after));
    }
}
