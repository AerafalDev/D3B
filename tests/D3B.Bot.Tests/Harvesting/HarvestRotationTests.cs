using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestRotationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private HarvestRotationZone Zone()
    {
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);

        return new HarvestRotationZone(TestPlay.SubAreaId, zone, new HarvestTour(new HarvestSightings(), new GraphicSkills()));
    }

    // The rule when no way to any zone is known: the order they were left in alone.
    private static HarvestRotationZone? NoWayKnown(IReadOnlyList<HarvestRotationZone> zones, DateTimeOffset now)
    {
        return HarvestRotation.Next(zones, now, null, (_, _) => null);
    }

    private static void GoThrough(HarvestRotationZone member)
    {
        foreach (var map in member.Zone.Maps.Keys)
            member.Tour.Visit(map);
    }

    [Fact]
    public void TheZoneLeftLongestAgoComesNextAndOneNeverLeftBeforeAnyOther()
    {
        var (first, second, third) = (Zone(), Zone(), Zone());

        first.CompletedAt = _now.AddMinutes(-5);
        second.CompletedAt = _now.AddMinutes(-9);

        Assert.Same(third, NoWayKnown([first, second, third], _now));

        third.CompletedAt = _now.AddMinutes(-1);

        Assert.Same(second, NoWayKnown([first, second, third], _now));
    }

    [Fact]
    public void TheDrawsOrderBreaksATie()
    {
        var (first, second) = (Zone(), Zone());

        Assert.Same(first, NoWayKnown([first, second], _now));
        Assert.Same(second, NoWayKnown([second, first], _now));
    }

    [Fact]
    public void AZoneWithNothingLeftToGoThroughOrSweptForNothingJustNowIsPassedOver()
    {
        var (gone, dry, ready) = (Zone(), Zone(), Zone());

        GoThrough(gone);
        dry.DryUntil = _now.AddSeconds(30);
        ready.CompletedAt = _now.AddMinutes(-1);

        Assert.Same(ready, NoWayKnown([gone, dry, ready], _now));

        // Once its wait is over the dry zone is a zone again, and the one never left comes before the one left a minute ago.
        Assert.Same(dry, NoWayKnown([gone, dry, ready], _now.AddSeconds(30)));
    }

    [Fact]
    public void OfTheZonesNotYetGoneToInTheRoundTheNearestComesNextHoweverLongAgoTheOthersWereLeft()
    {
        var (far, near, middle) = (Zone(), Zone(), Zone());
        var changes = new Dictionary<HarvestRotationZone, int?> { [far] = 74, [near] = 13, [middle] = 38 };

        far.CompletedAt = _now.AddHours(-2);
        near.CompletedAt = _now.AddMinutes(-20);

        Assert.Same(near, HarvestRotation.Next([far, near, middle], _now, null, (zone, _) => changes[zone]));
    }

    [Fact]
    public void AZoneGoneToInTheRoundComesAfterEveryZoneNotYetGoneToHoweverNearItIs()
    {
        var (gone, far) = (Zone(), Zone());
        var changes = new Dictionary<HarvestRotationZone, int?> { [gone] = 1, [far] = 90 };

        gone.Done = true;
        gone.CompletedAt = _now.AddMinutes(-30);
        far.CompletedAt = _now.AddMinutes(-10);

        Assert.Same(far, HarvestRotation.Next([gone, far], _now, null, (zone, _) => changes[zone]));
    }

    [Fact]
    public void OfTheZonesGoneToInTheRoundTheOneLeftLongestAgoComesFirstWhateverTheWay()
    {
        var (near, old, waiting) = (Zone(), Zone(), Zone());
        var changes = new Dictionary<HarvestRotationZone, int?> { [near] = 2, [old] = 60, [waiting] = 1 };

        near.Done = true;
        near.CompletedAt = _now.AddMinutes(-5);
        old.Done = true;
        old.CompletedAt = _now.AddMinutes(-40);
        GoThrough(waiting);

        Assert.Same(old, HarvestRotation.Next([near, old, waiting], _now, null, (zone, _) => changes[zone]));
    }

    [Fact]
    public void TheZoneJustLeftComesAfterTheOthersOfANewRoundThoughItIsWhereTheCharacterStands()
    {
        var (left, other) = (Zone(), Zone());
        var changes = new Dictionary<HarvestRotationZone, int?> { [left] = 0, [other] = 45 };

        left.CompletedAt = _now;
        other.CompletedAt = _now.AddMinutes(-15);

        Assert.Same(other, HarvestRotation.Next([left, other], _now, left, (zone, _) => changes[zone]));
        Assert.Same(left, HarvestRotation.Next([left], _now, left, (zone, _) => changes[zone]));
    }

    [Fact]
    public void AZoneNoWayLeadsToComesAfterTheZonesAWayLeadsTo()
    {
        var (unknown, far) = (Zone(), Zone());
        var changes = new Dictionary<HarvestRotationZone, int?> { [unknown] = null, [far] = 120 };

        Assert.Same(far, HarvestRotation.Next([unknown, far], _now, null, (zone, _) => changes[zone]));
    }

    [Fact]
    public void WhenEveryZoneIsGoneThroughOrDryNoneIsNextAndTheFirstWaitEndsTheWait()
    {
        var (gone, dry, later) = (Zone(), Zone(), Zone());

        GoThrough(gone);
        dry.DryUntil = _now.AddSeconds(40);
        GoThrough(later);
        later.DryUntil = _now.AddSeconds(90);

        Assert.Null(NoWayKnown([gone, dry, later], _now));
        Assert.Equal(_now.AddSeconds(40), HarvestRotation.WakeAt([gone, dry, later], _now));
        Assert.Equal(_now.AddSeconds(90), HarvestRotation.WakeAt([gone, dry, later], _now.AddSeconds(40)));
        Assert.Null(HarvestRotation.WakeAt([gone, dry, later], _now.AddSeconds(90)));
    }

    [Fact]
    public void WhenEveryZoneWouldWaitTheRotationHurriesToAMapWhoseShortestTimeIsPastThenCalmsDown()
    {
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
        var sightings = new HarvestSightings();
        var regrowths = new Regrowths();
        var growing = new HarvestRotationZone(TestPlay.SubAreaId, zone, new HarvestTour(sightings, new GraphicSkills(), regrowths, new FakeTimeProvider(_now)));
        var gone = Zone();

        // Ore known to grow back in 10 minutes, but reckoned 12 by a bound from below, harvested 11 minutes ago.
        regrowths.Remember(TestPlay.TreeSkillId, new Regrowth(null, 0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(12)));
        regrowths.Harvested(TestPlay.MapId, TestPlay.TreeElementId, TestPlay.TreeSkillId, _now.AddMinutes(-11));
        sightings.SeenElements(TestPlay.MapId, [new SightedElement(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestPlay.Ash, IsEnabled: false)], _now.AddMinutes(-11));

        foreach (var map in zone.Maps.Keys.Where(map => map != TestPlay.MapId))
            growing.Tour.Visit(map);

        GoThrough(gone);

        Assert.Null(NoWayKnown([gone, growing], _now));

        // Waiting there would do nothing: the map whose shortest time is past is gone to see instead.
        Assert.Same(growing, HarvestRotation.Hurry([gone, growing], _now, null, (_, _) => null));
        Assert.True(growing.Tour.Hurried);
        Assert.Equal([TestPlay.MapId], growing.Tour.Remaining(zone));

        HarvestRotation.Calm([gone, growing]);

        Assert.False(growing.Tour.Hurried);
        Assert.Empty(growing.Tour.Remaining(zone));
    }

    [Fact]
    public void WhenNothingIsToSeeEvenHurriedTheWaitEndsAtTheFirstShortestTime()
    {
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
        var sightings = new HarvestSightings();
        var regrowths = new Regrowths();
        var growing = new HarvestRotationZone(TestPlay.SubAreaId, zone, new HarvestTour(sightings, new GraphicSkills(), regrowths, new FakeTimeProvider(_now)));

        regrowths.Remember(TestPlay.TreeSkillId, new Regrowth(null, 0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(12)));
        regrowths.Harvested(TestPlay.MapId, TestPlay.TreeElementId, TestPlay.TreeSkillId, _now.AddMinutes(-4));
        sightings.SeenElements(TestPlay.MapId, [new SightedElement(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestPlay.Ash, IsEnabled: false)], _now.AddMinutes(-4));

        foreach (var map in zone.Maps.Keys.Where(map => map != TestPlay.MapId))
            growing.Tour.Visit(map);

        Assert.Equal(_now.AddMinutes(8), HarvestRotation.WakeAt([growing], _now));
        Assert.Null(HarvestRotation.Hurry([growing], _now, null, (_, _) => null));
        Assert.Equal(_now.AddMinutes(6), HarvestRotation.WakeAt([growing], _now));
    }
}
