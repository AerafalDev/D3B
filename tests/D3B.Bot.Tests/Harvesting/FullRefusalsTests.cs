using D3B.Bot.Harvesting;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>The harvests refused in a row over the pods, going on full, that end the harvest (D578).</summary>
public sealed class FullRefusalsTests
{
    [Fact]
    public void ThreeRefusalsInARowEndTheHarvest()
    {
        var refusals = new FullRefusals();

        Assert.False(refusals.Refused());
        Assert.False(refusals.Refused());
        Assert.True(refusals.Refused());
    }

    [Fact]
    public void AHarvestBetweenTwoRefusalsStartsThemAgain()
    {
        var refusals = new FullRefusals();

        refusals.Refused();
        refusals.Refused();
        refusals.Reset();

        Assert.False(refusals.Refused());
        Assert.False(refusals.Refused());
    }
}
