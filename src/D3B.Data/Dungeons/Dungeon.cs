using System.Runtime.InteropServices;

namespace D3B.Data.Dungeons;

/// <summary>A dungeon of the client's table: where its door is, its rooms, its key and its bosses (D520).</summary>
/// <remarks>
/// <para>
/// The client's <c>DungeonsDataRoot</c> record, the only place its data says where a dungeon's door is: the sub-areas' own
/// entrances and exits are empty (client.md). Its rooms, the objects it requires, its bosses and the sub-areas that name it
/// are slices of <see cref="DungeonCatalog"/>. The rooms are listed as the client lists them, which is not the order they are
/// walked in: the server moves a winner to the next room (client-game.md).
/// </para>
/// <para>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Dungeon
{
    /// <summary>Gets the identifier in the client's tables.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the least level the keeper lets in.</summary>
    public int MinLevel { get; }

    /// <summary>Gets the level the client advises for it.</summary>
    public int OptimalLevel { get; }

    /// <summary>Gets the map outside where its keeper stands.</summary>
    public long EntranceMapId { get; }

    /// <summary>Gets the map its exit leads to, often the entrance.</summary>
    public long ExitMapId { get; }

    /// <summary>Gets whether the keyring may open it instead of a key.</summary>
    public bool IsOnKeyring { get; }

    /// <summary>Gets the first of its rooms in <see cref="DungeonCatalog.Rooms"/>.</summary>
    public int FirstRoom { get; }

    /// <summary>Gets how many rooms it has.</summary>
    public int RoomCount { get; }

    /// <summary>Gets the first of the objects it requires in <see cref="DungeonCatalog.Objects"/>.</summary>
    public int FirstObject { get; }

    /// <summary>Gets how many objects it requires: none for an expedition.</summary>
    public int ObjectCount { get; }

    /// <summary>Gets the first of its bosses in <see cref="DungeonCatalog.Bosses"/>.</summary>
    public int FirstBoss { get; }

    /// <summary>Gets how many bosses it has.</summary>
    public int BossCount { get; }

    /// <summary>Gets the first of the sub-areas that name it in <see cref="DungeonCatalog.SubAreas"/>.</summary>
    public int FirstSubArea { get; }

    /// <summary>Gets how many sub-areas name it.</summary>
    public int SubAreaCount { get; }

    /// <summary>Initializes a new instance of the <see cref="Dungeon"/> struct.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="minLevel">The least level.</param>
    /// <param name="optimalLevel">The advised level.</param>
    /// <param name="entranceMapId">The keeper's map.</param>
    /// <param name="exitMapId">Where the exit leads.</param>
    /// <param name="isOnKeyring">Whether the keyring opens it.</param>
    /// <param name="firstRoom">The first of its rooms.</param>
    /// <param name="roomCount">How many rooms.</param>
    /// <param name="firstObject">The first of the objects it requires.</param>
    /// <param name="objectCount">How many objects.</param>
    /// <param name="firstBoss">The first of its bosses.</param>
    /// <param name="bossCount">How many bosses.</param>
    /// <param name="firstSubArea">The first of the sub-areas naming it.</param>
    /// <param name="subAreaCount">How many sub-areas.</param>
    public Dungeon(int id, int nameId, int minLevel, int optimalLevel, long entranceMapId, long exitMapId, bool isOnKeyring, int firstRoom, int roomCount, int firstObject, int objectCount, int firstBoss, int bossCount, int firstSubArea, int subAreaCount)
    {
        Id = id;
        NameId = nameId;
        MinLevel = minLevel;
        OptimalLevel = optimalLevel;
        EntranceMapId = entranceMapId;
        ExitMapId = exitMapId;
        IsOnKeyring = isOnKeyring;
        FirstRoom = firstRoom;
        RoomCount = roomCount;
        FirstObject = firstObject;
        ObjectCount = objectCount;
        FirstBoss = firstBoss;
        BossCount = bossCount;
        FirstSubArea = firstSubArea;
        SubAreaCount = subAreaCount;
    }
}
