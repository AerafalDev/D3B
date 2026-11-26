using D3B.Bot.Harvesting;
using D3B.Data.Tests;
using D3B.Storage.Harvests;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Harvests;

public sealed class SightingRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task WhatEachMapHoldsIsKnownFromTheNextRunsStartEachElementTakenAsOfferingItsSkill()
    {
        var path = _directory.Combine("d3b.db");
        var start = new DateTimeOffset(2026, 11, 26, 14, 29, 0, TimeSpan.Zero);

        await using (var first = await new TestStorage(path).StartedAsync())
        {
            var sightings = first.Services.GetRequiredService<HarvestSightings>();

            sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: false), new SightedElement(11, 24, 312, IsEnabled: true)], start);
            sightings.SeenElements(2, [], start);
            sightings.SeenElements(3, [], start);
            await first.Services.GetRequiredService<SightingRecorder>().WrittenAsync();

            // Harvested and grown back since, it holds the same: nothing more is written.
            sightings.SeenElements(1, [new SightedElement(10, 24, 312, IsEnabled: true), new SightedElement(11, 24, 312, IsEnabled: false)], start.AddMinutes(5));

            // Seen empty, then holding a resource: the row is written again.
            sightings.SeenElements(3, [new SightedElement(30, 24, 312, IsEnabled: true)], start.AddMinutes(6));
            await first.Services.GetRequiredService<SightingRecorder>().WrittenAsync();
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var known = next.Services.GetRequiredService<HarvestSightings>();

        Assert.Equal(3, known.Count);
        Assert.True(known.TryGet(1, out var mine));
        Assert.Equal([312], mine.Resources);
        Assert.Equal([new SightedElement(10, 24, 312, IsEnabled: true), new SightedElement(11, 24, 312, IsEnabled: true)], mine.Elements.OrderBy(element => element.ElementId));
        Assert.Equal(start, mine.At);
        Assert.True(known.IsEmptyOf(2, new HashSet<int> { 312 }));
        Assert.True(known.TryGet(3, out var grown));
        Assert.Equal([new SightedElement(30, 24, 312, IsEnabled: true)], grown.Elements);
        Assert.Contains(next.Log.GetSnapshot(), record => record.Message.Contains(", 3 maps' resources,", StringComparison.Ordinal));
    }
}
