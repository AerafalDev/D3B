using D3B.Data.Tests.Packs;
using D3B.Data.World;

namespace D3B.Data.Tests.World;

public sealed class WorldAtlasTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void ARecordIsFoundByItsIdentifier()
    {
        var atlas = TestWorld.Atlas();

        Assert.True(atlas.TryGetPosition(200, out var position));
        Assert.Equal((5, -18, 10), (position.X, position.Y, position.SubAreaId));
        Assert.False(atlas.TryGetPosition(250, out _));
        Assert.True(atlas.TryGetSubArea(11, out var subArea));
        Assert.True(subArea.BasicAccountAllowed);
        Assert.Equal(SubArea.NoZaap, subArea.AssociatedZaapMapId);
        Assert.True(atlas.TryGetArea(1, out var area));
        Assert.Equal(9001, area.NameId);
        Assert.True(atlas.TryGetWaypoint(5, out var waypoint));
        Assert.Equal(100, waypoint.MapId);
        Assert.False(atlas.TryGetWaypoint(6, out _));
    }

    [Fact]
    public void ATableOutOfOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorldAtlas(
            new MapPosition[] { new(200, 0, 0, 1, 1, 1, 0), new(100, 0, 0, 1, 1, 1, 0) },
            ReadOnlyMemory<SubArea>.Empty,
            ReadOnlyMemory<Area>.Empty,
            ReadOnlyMemory<Waypoint>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldAtlas(
            ReadOnlyMemory<MapPosition>.Empty,
            ReadOnlyMemory<SubArea>.Empty,
            new Area[] { new(1, 1, 0), new(1, 2, 0) },
            ReadOnlyMemory<Waypoint>.Empty));
    }

    [Fact]
    public void AnAtlasReadsBackFromAPack()
    {
        var atlas = TestWorld.Atlas();

        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add(WorldAtlas.EntryName, atlas)));

        var read = pack.Read<WorldAtlas>(WorldAtlas.EntryName);

        Assert.Equal(atlas.Positions.ToArray(), read.Positions.ToArray());
        Assert.Equal(atlas.SubAreas.ToArray(), read.SubAreas.ToArray());
        Assert.Equal(atlas.Areas.ToArray(), read.Areas.ToArray());
        Assert.Equal(atlas.Waypoints.ToArray(), read.Waypoints.ToArray());
    }
}
