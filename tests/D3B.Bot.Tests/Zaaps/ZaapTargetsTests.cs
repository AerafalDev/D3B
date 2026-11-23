using D3B.Bot.Zaaps;
using D3B.Data.World;

namespace D3B.Bot.Tests.Zaaps;

public sealed class ZaapTargetsTests
{
    private static WorldAtlas Atlas()
    {
        Waypoint[] waypoints = [new(1, 100, 10, true), new(2, 200, 20, true), new(3, 300, 30, false)];

        return new WorldAtlas(ReadOnlyMemory<MapPosition>.Empty, ReadOnlyMemory<SubArea>.Empty, ReadOnlyMemory<Area>.Empty, waypoints, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<int>.Empty);
    }

    [Fact]
    public void TheZaapsSoughtAreTheActivatedOnesTheCharacterDoesNotKnow()
    {
        var sought = ZaapTargets.Of(Atlas(), new HashSet<long> { 100 });

        Assert.Equal([200L], sought.Keys.Order());
    }

    [Fact]
    public void AZaapSoughtNamesItsSubArea()
    {
        var sought = ZaapTargets.Of(Atlas(), new HashSet<long>());

        Assert.Equal(20, sought[200]);
    }
}
