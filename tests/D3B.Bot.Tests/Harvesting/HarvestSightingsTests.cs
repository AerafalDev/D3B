using D3B.Bot.Harvesting;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestSightingsTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 26, 14, 29, 0, TimeSpan.Zero);

    [Fact]
    public void AMapIsToldLearnedOnlyWhenWhatItHoldsChangesNotWhenAnElementIsHarvestedOrGrowsBack()
    {
        var sightings = new HarvestSightings();
        var told = new List<long>();

        sightings.Learned += told.Add;
        sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: true)], _start);
        sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: false)], _start.AddSeconds(3));
        sightings.SeenElements(2, [], _start);
        sightings.SeenElements(2, [], _start.AddMinutes(1));
        sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: true), new SightedElement(12, 24, 312, IsEnabled: true)], _start.AddMinutes(9));

        Assert.Equal([1, 2, 1], told);
    }

    [Fact]
    public void AMapTakenBackIsKnownWithoutBeingToldAndNeverOverWhatABotSawSince()
    {
        var sightings = new HarvestSightings();
        var told = new List<long>();

        sightings.Learned += told.Add;
        sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: false)], _start);
        sightings.Remember(1, [new SightedElement(10, 24, 312, IsEnabled: true), new SightedElement(11, 24, 312, IsEnabled: true)], _start.AddDays(-1));
        sightings.Remember(2, [new SightedElement(20, 24, 312, IsEnabled: true)], _start.AddDays(-1));

        Assert.True(sightings.TryGet(1, out var seen));
        Assert.Equal(_start, seen.At);
        Assert.True(sightings.TryGet(2, out var kept));
        Assert.Equal([312], kept.Resources);
        Assert.Equal([1], told);

        // Seen again as the database kept it, nothing to write.
        sightings.SeenElements(2, [new SightedElement(20, 24, 312, IsEnabled: false)], _start);

        Assert.Equal([1], told);
    }
}
