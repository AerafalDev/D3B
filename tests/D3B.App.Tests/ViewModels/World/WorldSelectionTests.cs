using D3B.App.ViewModels.World;

namespace D3B.App.Tests.ViewModels.World;

public sealed class WorldSelectionTests
{
    [Fact]
    public void SelectingARouteWithAStepHoldsThatStepAlone()
    {
        var selection = new WorldSelection();

        selection.SelectRoute("north", 2);

        Assert.Equal(2, selection.Step);
        Assert.Equal([2], selection.Steps);
        Assert.True(selection.IsStep("north", 2));
        Assert.False(selection.IsStep("north", 1));
        Assert.False(selection.IsStep("south", 2));
    }

    [Fact]
    public void SelectingARouteWithNoStepHoldsNoStep()
    {
        var selection = new WorldSelection();

        selection.SelectRoute("north");

        Assert.Equal(-1, selection.Step);
        Assert.Empty(selection.Steps);
    }

    [Fact]
    public void SeveralStepsAreHeldInOrderWithTheOneChosenLastAsTheOneTheDetailsShow()
    {
        var selection = new WorldSelection();

        selection.SelectSteps("north", [3, 1, 3, 2], 1);

        Assert.Equal([1, 2, 3], selection.Steps);
        Assert.Equal(1, selection.Step);
        Assert.True(selection.IsStep("north", 3));
    }

    [Fact]
    public void ChangingTheStepsAloneIsToldAndTheSameStepsAreNot()
    {
        var selection = new WorldSelection();
        var told = 0;

        selection.Changed += () => told++;
        selection.SelectSteps("north", [1, 2], 2);
        selection.SelectSteps("north", [2, 1], 2);
        selection.SelectSteps("north", [1, 2, 3], 2);

        Assert.Equal(2, told);
    }

    [Fact]
    public void SelectingSomethingElseDropsTheSteps()
    {
        var selection = new WorldSelection();

        selection.SelectSteps("north", [1, 2], 2);
        selection.SelectZone("forest");

        Assert.Empty(selection.Steps);
        Assert.Equal(-1, selection.Step);
    }
}
