using D3B.Bot.Characters;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>
/// The maps of a zone no way leads to, by the world graph and the character's rules: measured on the owner's farmer of 25 and 26
/// November, whose sub-area held one map it could reach among 34, the others behind a door's criterion; each new tour sought them
/// again, the journey found no way, and the character stood still until a group of the map attacked it.
/// </summary>
public sealed class HarvestTourNoWayTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 8, 45, 0, TimeSpan.Zero));

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private HarvestTour Tour()
    {
        return new HarvestTour(new HarvestSightings(), new GraphicSkills(), time: _time);
    }

    private HarvestZone Zone()
    {
        return HarvestZone.Of(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
    }

    private static CharacterState Character(int level, DateTimeOffset? subscriptionEnd = null)
    {
        return new CharacterState { Key = TestPlay.Key, Name = "Character", Level = level, SubscriptionEnd = subscriptionEnd };
    }

    [Fact]
    public void AMapNoWayLeadsToStaysOutOfTheNextTour()
    {
        var zone = Zone();
        var tour = Tour();

        Assert.Contains(TestPlay.NorthMapId, tour.Remaining(zone));

        tour.NoWay(TestPlay.NorthMapId);

        Assert.DoesNotContain(TestPlay.NorthMapId, tour.Remaining(zone));
        Assert.True(tour.Next(zone));
        Assert.DoesNotContain(TestPlay.NorthMapId, tour.Remaining(zone));
    }

    [Fact]
    public void AMapWhoseJourneyFailedComesBackInTheNextTour()
    {
        var zone = Zone();
        var tour = Tour();

        tour.Unreach(TestPlay.NorthMapId);

        Assert.True(tour.Next(zone));
        Assert.Contains(TestPlay.NorthMapId, tour.Remaining(zone));
    }

    [Fact]
    public void AMapNoWayLeadsToComesBackOnceTheCharactersLevelChanges()
    {
        var zone = Zone();
        var tour = Tour();

        tour.Know(Character(83));
        tour.NoWay(TestPlay.NorthMapId);
        tour.Next(zone);
        tour.Know(Character(83));

        Assert.DoesNotContain(TestPlay.NorthMapId, tour.Remaining(zone));

        tour.Know(Character(84));

        Assert.Contains(TestPlay.NorthMapId, tour.Remaining(zone));
    }

    [Fact]
    public void AMapNoWayLeadsToComesBackOnceTheAccountIsSubscribed()
    {
        var zone = Zone();
        var tour = Tour();

        tour.Know(Character(83));
        tour.NoWay(TestPlay.NorthMapId);
        tour.Know(Character(83, _time.GetUtcNow().AddDays(30)));

        Assert.Contains(TestPlay.NorthMapId, tour.Remaining(zone));
    }

    [Fact]
    public void AZoneNoWayLeadsToAnyMapOfHasNoTourLeft()
    {
        var zone = Zone();
        var tour = Tour();

        foreach (var map in zone.Maps.Keys)
            tour.NoWay(map);

        Assert.Empty(tour.Remaining(zone));
        Assert.Equal(0, tour.Total(zone));
        Assert.False(tour.Next(zone));
        Assert.True(tour.HasNoWay);
    }
}
