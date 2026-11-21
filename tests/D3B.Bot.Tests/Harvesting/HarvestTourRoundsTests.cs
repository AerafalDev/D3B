using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>A tour that goes round without getting anywhere (D487): journeys that arrive with nothing harvested and no map gone through, left, or crossed between them.</summary>
public sealed class HarvestTourRoundsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static HarvestTour Tour()
    {
        return new HarvestTour(new HarvestSightings(), new GraphicSkills());
    }

    private HarvestZone Zone()
    {
        return HarvestZone.Of(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
    }

    [Fact]
    public void ATourGoesRoundOnceThreeJourneysInARowArriveWithNothingDoneSinceTheLast()
    {
        // Measured in a mine (D487): each round a journey arrived on the map whose ore the character never reached, 58 times.
        var tour = Tour();

        Assert.False(tour.Arrived());
        Assert.False(tour.Arrived());
        Assert.False(tour.Arrived());
        Assert.True(tour.Arrived());
        Assert.Equal(3, tour.IdleArrivals);
    }

    [Theory]
    [InlineData("visit")]
    [InlineData("harvest")]
    [InlineData("unreach")]
    [InlineData("cross")]
    [InlineData("enter")]
    public void AJourneyArrivingWithSomethingDoneSinceTheLastIsNoRound(string done)
    {
        var tour = Tour();

        tour.Arrived();
        tour.Arrived();
        tour.Arrived();

        switch (done)
        {
            case "visit":
                tour.Visit(TestPlay.MapId);
                break;
            case "harvest":
                tour.Harvested();
                break;
            case "unreach":
                tour.Unreach(TestPlay.NorthMapId);
                break;
            case "cross":
                tour.Cross(TestPlay.EastMapId);
                break;
            default:
                tour.Enter(7);
                break;
        }

        Assert.False(tour.Arrived());
        Assert.Equal(0, tour.IdleArrivals);
    }

    [Fact]
    public void EveryHarvestIsSomethingDoneNotTheFirstAlone()
    {
        var tour = Tour();

        tour.Harvested();
        tour.Arrived();
        tour.Harvested();

        Assert.False(tour.Arrived());
        tour.Harvested();
        Assert.False(tour.Arrived());
        Assert.Equal(0, tour.IdleArrivals);
    }

    [Fact]
    public void ATourGivenUpLeavesEveryMapStillToGoThroughAndTheNextTourTakesThemBack()
    {
        var zone = Zone();
        var tour = Tour();

        tour.Visit(TestPlay.MapId);
        tour.WantZone(7);
        tour.GiveUpRemaining(zone);

        Assert.Empty(tour.Remaining(zone));
        Assert.Empty(tour.OtherZones);
        Assert.True(tour.IsVisited(TestPlay.MapId));

        tour.Arrived();
        tour.Arrived();
        tour.Arrived();

        Assert.True(tour.Next(zone));
        Assert.NotEmpty(tour.Remaining(zone));
        Assert.Equal(0, tour.IdleArrivals);
    }
}
