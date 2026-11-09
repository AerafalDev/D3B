using D3B.Bot.Movement;

namespace D3B.Bot.Tests.Movement;

public sealed class JourneyBudgetTests
{
    [Fact]
    public void AJourneyMayMakeTwiceTheChangesOfItsFirstPathAndTenMore()
    {
        var budget = new JourneyBudget();

        budget.Open(0, 3);

        Assert.False(budget.IsSpent(15));
        Assert.True(budget.IsSpent(16));
    }

    [Fact]
    public void ALaterPathDoesNotChangeTheBudget()
    {
        var budget = new JourneyBudget();

        budget.Open(0, 1);
        budget.Open(5, 40);

        Assert.True(budget.IsSpent(12));
    }

    [Fact]
    public void AZaapThatFailedLeavesTheRoomForTheLongWayOnFoot()
    {
        // A zaap to the bank was one step: 12 changes at most. The server refused it, and the walk is 40 maps.
        var budget = new JourneyBudget();

        budget.Open(0, 1);
        budget.Renew();
        budget.Open(1, 40);

        Assert.False(budget.IsSpent(12));
        Assert.False(budget.IsSpent(90));
        Assert.True(budget.IsSpent(91));
    }
}
