using D3B.Data.Dungeons;

namespace D3B.Data.Tests.Dungeons;

public sealed class DungeonCatalogTests
{
    private static readonly long[] _rooms = [1, 2];
    private static readonly long[] _oneRoom = [1];
    private static readonly int[] _none = [];
    private static readonly int[] _descending = [2, 1];

    [Fact]
    public void ADungeonIsFoundByItsIdWithItsRoomsKeysBossesAndSubAreas()
    {
        var catalog = TestTables.Dungeons();

        Assert.True(catalog.TryGetDungeon(1, out var dungeon));
        Assert.Equal(20, dungeon.MinLevel);
        Assert.Equal(192_937_992, dungeon.EntranceMapId);
        Assert.True(dungeon.IsOnKeyring);
        Assert.Equal([190_317_570, 190_318_082], catalog.RoomsOf(dungeon).ToArray());
        Assert.Equal([new DungeonObject(1_568, 1)], catalog.ObjectsOf(dungeon).ToArray());
        Assert.Equal([147], catalog.BossesOf(dungeon).ToArray());
        Assert.Equal([509], catalog.SubAreasOf(dungeon).ToArray());
    }

    [Fact]
    public void ARoomNamesItsDungeonAndAMapOfNoneNamesNothing()
    {
        var catalog = TestTables.Dungeons();

        Assert.True(catalog.TryGetDungeonOfRoom(26_738_688, out var dungeon));
        Assert.Equal(7, dungeon.Id);
        Assert.False(catalog.TryGetDungeonOfRoom(192_937_992, out _));
        Assert.False(catalog.TryGetDungeon(2, out _));
    }

    [Fact]
    public void TheKeepersAndTheExitsRepliesAreKnownByTheirKind()
    {
        var catalog = TestTables.Dungeons();

        Assert.True(catalog.IsKeyReply(2_802));
        Assert.True(catalog.IsKeyringReply(6_615));
        Assert.True(catalog.IsConfirmation(20_904));
        Assert.True(catalog.IsExitReply(15_920));
        Assert.False(catalog.IsKeyReply(6_615));
        Assert.False(catalog.IsExitReply(20_904));
    }

    [Fact]
    public void ATableOutOfOrderOrWhoseSlicesDoNotFollowIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DungeonCatalog(
            new Dungeon[] { new(2, 0, 1, 1, 10, 10, false, 0, 1, 0, 0, 0, 0, 0, 0), new(1, 0, 1, 1, 10, 10, false, 1, 1, 0, 0, 0, 0, 0, 0) },
            _rooms,
            Array.Empty<DungeonObject>(),
            _none,
            _none,
            _none,
            _none,
            _none,
            _none));
        Assert.Throws<ArgumentException>(() => new DungeonCatalog(
            new Dungeon[] { new(1, 0, 1, 1, 10, 10, false, 0, 2, 0, 0, 0, 0, 0, 0) },
            _oneRoom,
            Array.Empty<DungeonObject>(),
            _none,
            _none,
            _none,
            _none,
            _none,
            _none));
        Assert.Throws<ArgumentException>(() => new DungeonCatalog(
            new Dungeon[] { new(1, 0, 1, 1, 10, 10, false, 0, 1, 0, 0, 0, 0, 0, 0) },
            _oneRoom,
            Array.Empty<DungeonObject>(),
            _none,
            _none,
            _descending,
            _none,
            _none,
            _none));
    }
}
