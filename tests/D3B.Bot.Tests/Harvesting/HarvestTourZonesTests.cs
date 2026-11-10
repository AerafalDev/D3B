using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>What a tour keeps of the other zones of a map it left resources in (D425): vertices, once each, while its visited maps stay keyed by map.</summary>
public sealed class HarvestTourZonesTests : IAsyncDisposable
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

    [Fact]
    public void AZoneWantedIsKeptUntilTheCharacterStandsInIt()
    {
        var tour = Tour();

        tour.WantZone(7);
        tour.WantZone(9);
        tour.WantZone(7);

        Assert.Equal([7, 9], tour.OtherZones.Order());

        tour.Enter(7);

        Assert.Equal([9], tour.OtherZones);
    }

    [Fact]
    public void AZoneTheCharacterStoodInIsNotWantedAgainInTheSameTour()
    {
        var tour = Tour();

        tour.Enter(7);
        tour.WantZone(7);

        Assert.Empty(tour.OtherZones);
    }

    [Fact]
    public void AZoneEnteredOrGivenUpIsSaidSo()
    {
        var tour = Tour();

        tour.Enter(7);
        tour.GiveUp(8);

        Assert.True(tour.IsEntered(7));
        Assert.True(tour.IsEntered(8));
        Assert.False(tour.IsEntered(9));
    }

    [Fact]
    public void AZoneGivenUpIsNotWantedAgainInTheSameTour()
    {
        var tour = Tour();

        tour.WantZone(7);
        tour.GiveUp(7);
        tour.WantZone(7);

        Assert.Empty(tour.OtherZones);
    }

    [Fact]
    public void TheNextTourWantsTheZonesAgain()
    {
        var zone = HarvestZone.Of(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
        var tour = Tour();

        tour.WantZone(7);
        tour.Enter(8);
        tour.GiveUp(9);

        Assert.True(tour.Next(zone));
        Assert.Empty(tour.OtherZones);

        tour.WantZone(7);
        tour.WantZone(8);
        tour.WantZone(9);

        Assert.Equal([7, 8, 9], tour.OtherZones.Order());
    }

    [Fact]
    public void AZoneWantedLeavesTheMapsOfTheTourAlone()
    {
        var zone = HarvestZone.Of(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
        var tour = Tour();

        tour.Visit(TestPlay.MapId);
        tour.WantZone(7);

        Assert.True(tour.IsVisited(TestPlay.MapId));
        Assert.Equal(1, tour.Visited);
        Assert.DoesNotContain(TestPlay.MapId, tour.Remaining(zone));
        Assert.Equal(3, tour.Total(zone));
    }
}
