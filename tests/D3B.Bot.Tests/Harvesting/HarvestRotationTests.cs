using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;

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

        Assert.Same(third, HarvestRotation.Next([first, second, third], _now));

        third.CompletedAt = _now.AddMinutes(-1);

        Assert.Same(second, HarvestRotation.Next([first, second, third], _now));
    }

    [Fact]
    public void TheDrawsOrderBreaksATie()
    {
        var (first, second) = (Zone(), Zone());

        Assert.Same(first, HarvestRotation.Next([first, second], _now));
        Assert.Same(second, HarvestRotation.Next([second, first], _now));
    }

    [Fact]
    public void AZoneWithNothingLeftToGoThroughOrSweptForNothingJustNowIsPassedOver()
    {
        var (gone, dry, ready) = (Zone(), Zone(), Zone());

        GoThrough(gone);
        dry.DryUntil = _now.AddSeconds(30);
        ready.CompletedAt = _now.AddMinutes(-1);

        Assert.Same(ready, HarvestRotation.Next([gone, dry, ready], _now));

        // Once its wait is over the dry zone is a zone again, and the one never left comes before the one left a minute ago.
        Assert.Same(dry, HarvestRotation.Next([gone, dry, ready], _now.AddSeconds(30)));
    }

    [Fact]
    public void WhenEveryZoneIsGoneThroughOrDryNoneIsNextAndTheFirstWaitEndsTheWait()
    {
        var (gone, dry, later) = (Zone(), Zone(), Zone());

        GoThrough(gone);
        dry.DryUntil = _now.AddSeconds(40);
        GoThrough(later);
        later.DryUntil = _now.AddSeconds(90);

        Assert.Null(HarvestRotation.Next([gone, dry, later], _now));
        Assert.Equal(_now.AddSeconds(40), HarvestRotation.WakeAt([gone, dry, later], _now));
        Assert.Equal(_now.AddSeconds(90), HarvestRotation.WakeAt([gone, dry, later], _now.AddSeconds(40)));
        Assert.Null(HarvestRotation.WakeAt([gone, dry, later], _now.AddSeconds(90)));
    }
}
