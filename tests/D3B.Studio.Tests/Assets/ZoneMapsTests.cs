using D3B.Data.World;
using D3B.Studio.Assets;

namespace D3B.Studio.Tests.Assets;

public sealed class ZoneMapsTests
{
    // Maps 1 to 3 lie in sub-area 10, maps 4 and 5 in sub-area 20, map 6 in sub-area 30.
    private static WorldAtlas Atlas()
    {
        MapPosition[] positions =
        [
            new(1, 1, 0, 10, 1, 0, 0),
            new(2, 2, 0, 10, 1, 0, 0),
            new(3, 3, 0, 10, 1, 0, 0),
            new(4, 4, 0, 20, 1, 0, 0),
            new(5, 5, 0, 20, 1, 0, 0),
            new(6, 6, 0, 30, 1, 0, 0),
        ];
        SubArea[] subAreas = [new(10, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0), new(20, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0), new(30, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }

    [Fact]
    public void AZoneHoldsTheMapsOfItsSubAreasAndTheMapsNamedLessTheOnesLeftOut()
    {
        var zone = new ZoneAsset { Id = "z", Name = "North", SubAreas = [10], Maps = [5, 6], Excluded = [2, 6] };
        var maps = ZoneMaps.Resolve(zone, Atlas());

        Assert.Equal([1L, 3L, 5L], maps.Order());
    }

    [Fact]
    public void AMapIsHeldByItsSubAreaOrByItsNameAndNeverWhenLeftOutOrUnknown()
    {
        var atlas = Atlas();
        var zone = new ZoneAsset { Id = "z", Name = "North", SubAreas = [20], Maps = [1], Excluded = [5] };

        Assert.True(ZoneMaps.Holds(zone, atlas, 4));
        Assert.True(ZoneMaps.Holds(zone, atlas, 1));
        Assert.False(ZoneMaps.Holds(zone, atlas, 5));
        Assert.False(ZoneMaps.Holds(zone, atlas, 2));
        Assert.False(ZoneMaps.Holds(zone, atlas, 99));
    }

    [Fact]
    public void AZoneWithNothingHoldsNothing()
    {
        Assert.Empty(ZoneMaps.Resolve(new ZoneAsset { Id = "z", Name = "Empty" }, Atlas()));
    }
}
