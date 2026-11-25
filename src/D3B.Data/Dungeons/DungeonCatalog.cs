using MemoryPack;

namespace D3B.Data.Dungeons;

/// <summary>The client's dungeons, and the replies of their keepers and their exits (D520, D522).</summary>
/// <remarks>
/// <para>
/// 187 dungeons of client 3.7.14.18, sorted by identifier and found by binary search, each with its slices of rooms, required
/// objects, bosses and the sub-areas that name it. A room belongs to one dungeon.
/// </para>
/// <para>
/// A keeper's replies are its own ids, one text each, so that no id and no text id is shared from keeper to keeper: the
/// extraction finds them by their French words, "Donner la clef et entrer.", "Utiliser le trousseau de clefs.", the "Oui." of a
/// keeper and the exits' "Sortir.", once for every client, so that the bot answers by id whatever the client's language
/// (client.md). Each list is sorted.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class DungeonCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "dungeons/catalog";

    /// <summary>The keyring, "Trousseau de clefs", an item that opens the dungeons on it without spending a key (client.md).</summary>
    public const int KeyringItemId = 10_207;

    /// <summary>Gets the dungeons, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Dungeon> Dungeons { get; }

    /// <summary>Gets the dungeons' rooms, sliced by <see cref="Dungeon.FirstRoom"/> and <see cref="Dungeon.RoomCount"/>.</summary>
    public ReadOnlyMemory<long> Rooms { get; }

    /// <summary>Gets the objects the dungeons require, sliced by <see cref="Dungeon.FirstObject"/> and <see cref="Dungeon.ObjectCount"/>.</summary>
    public ReadOnlyMemory<DungeonObject> Objects { get; }

    /// <summary>Gets the dungeons' bosses, monsters by id, sliced by <see cref="Dungeon.FirstBoss"/> and <see cref="Dungeon.BossCount"/>.</summary>
    public ReadOnlyMemory<int> Bosses { get; }

    /// <summary>Gets the sub-areas that name each dungeon, sliced by <see cref="Dungeon.FirstSubArea"/> and <see cref="Dungeon.SubAreaCount"/>.</summary>
    public ReadOnlyMemory<int> SubAreas { get; }

    /// <summary>Gets the keepers' replies that give a key and enter, in ascending order.</summary>
    public ReadOnlyMemory<int> KeyReplies { get; }

    /// <summary>Gets the keepers' replies that use the keyring, in ascending order.</summary>
    public ReadOnlyMemory<int> KeyringReplies { get; }

    /// <summary>Gets the keepers' replies that confirm an entry, in ascending order.</summary>
    public ReadOnlyMemory<int> Confirmations { get; }

    /// <summary>Gets the replies that leave a dungeon, in ascending order.</summary>
    public ReadOnlyMemory<int> ExitReplies { get; }

    /// <summary>Initializes a new instance of the <see cref="DungeonCatalog"/> class.</summary>
    /// <param name="dungeons">The dungeons, in strictly ascending order of identifier.</param>
    /// <param name="rooms">The rooms, in the order of their slices.</param>
    /// <param name="objects">The required objects, in the order of their slices.</param>
    /// <param name="bosses">The bosses, in the order of their slices.</param>
    /// <param name="subAreas">The sub-areas, in the order of their slices.</param>
    /// <param name="keyReplies">The replies giving a key, ascending.</param>
    /// <param name="keyringReplies">The replies using the keyring, ascending.</param>
    /// <param name="confirmations">The replies confirming an entry, ascending.</param>
    /// <param name="exitReplies">The replies leaving a dungeon, ascending.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or the slices do not follow one another.</exception>
    [MemoryPackConstructor]
    public DungeonCatalog(
        ReadOnlyMemory<Dungeon> dungeons,
        ReadOnlyMemory<long> rooms,
        ReadOnlyMemory<DungeonObject> objects,
        ReadOnlyMemory<int> bosses,
        ReadOnlyMemory<int> subAreas,
        ReadOnlyMemory<int> keyReplies,
        ReadOnlyMemory<int> keyringReplies,
        ReadOnlyMemory<int> confirmations,
        ReadOnlyMemory<int> exitReplies)
    {
        Tables.CheckAscending(dungeons.Span, static dungeon => dungeon.Id, nameof(dungeons));
        Tables.CheckSlices(dungeons.Span, static dungeon => (dungeon.FirstRoom, dungeon.RoomCount), rooms.Length, nameof(rooms));
        Tables.CheckSlices(dungeons.Span, static dungeon => (dungeon.FirstObject, dungeon.ObjectCount), objects.Length, nameof(objects));
        Tables.CheckSlices(dungeons.Span, static dungeon => (dungeon.FirstBoss, dungeon.BossCount), bosses.Length, nameof(bosses));
        Tables.CheckSlices(dungeons.Span, static dungeon => (dungeon.FirstSubArea, dungeon.SubAreaCount), subAreas.Length, nameof(subAreas));
        Tables.CheckAscending(keyReplies.Span, static reply => reply, nameof(keyReplies));
        Tables.CheckAscending(keyringReplies.Span, static reply => reply, nameof(keyringReplies));
        Tables.CheckAscending(confirmations.Span, static reply => reply, nameof(confirmations));
        Tables.CheckAscending(exitReplies.Span, static reply => reply, nameof(exitReplies));

        Dungeons = dungeons;
        Rooms = rooms;
        Objects = objects;
        Bosses = bosses;
        SubAreas = subAreas;
        KeyReplies = keyReplies;
        KeyringReplies = keyringReplies;
        Confirmations = confirmations;
        ExitReplies = exitReplies;
    }

    /// <summary>Finds a dungeon.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="dungeon">The dungeon, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetDungeon(int id, out Dungeon dungeon)
    {
        return Tables.TryFind(Dungeons.Span, id, static dungeon => dungeon.Id, out dungeon);
    }

    /// <summary>Finds the dungeon a map is a room of.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="dungeon">The dungeon, when the map is one of its rooms.</param>
    /// <returns>Whether the map is a dungeon's room.</returns>
    public bool TryGetDungeonOfRoom(long mapId, out Dungeon dungeon)
    {
        var rooms = Rooms.Span;

        foreach (var candidate in Dungeons.Span)
        {
            if (rooms.Slice(candidate.FirstRoom, candidate.RoomCount).Contains(mapId))
            {
                dungeon = candidate;

                return true;
            }
        }

        dungeon = default;

        return false;
    }

    /// <summary>Gets a dungeon's rooms, as the client lists them.</summary>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Its rooms' maps.</returns>
    public ReadOnlySpan<long> RoomsOf(Dungeon dungeon)
    {
        return Rooms.Span.Slice(dungeon.FirstRoom, dungeon.RoomCount);
    }

    /// <summary>Gets the objects a dungeon requires.</summary>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Its keys, and how many of each.</returns>
    public ReadOnlySpan<DungeonObject> ObjectsOf(Dungeon dungeon)
    {
        return Objects.Span.Slice(dungeon.FirstObject, dungeon.ObjectCount);
    }

    /// <summary>Gets a dungeon's bosses.</summary>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Its bosses, monsters by id.</returns>
    public ReadOnlySpan<int> BossesOf(Dungeon dungeon)
    {
        return Bosses.Span.Slice(dungeon.FirstBoss, dungeon.BossCount);
    }

    /// <summary>Gets the sub-areas that name a dungeon, which hold its rooms and the map its boss's win leads to.</summary>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>The sub-areas.</returns>
    public ReadOnlySpan<int> SubAreasOf(Dungeon dungeon)
    {
        return SubAreas.Span.Slice(dungeon.FirstSubArea, dungeon.SubAreaCount);
    }

    /// <summary>Gets whether a reply gives a keeper a key and enters.</summary>
    /// <param name="replyId">The reply, by id.</param>
    /// <returns>Whether it is one.</returns>
    public bool IsKeyReply(int replyId)
    {
        return KeyReplies.Span.BinarySearch(replyId) >= 0;
    }

    /// <summary>Gets whether a reply enters with the keyring.</summary>
    /// <param name="replyId">The reply, by id.</param>
    /// <returns>Whether it is one.</returns>
    public bool IsKeyringReply(int replyId)
    {
        return KeyringReplies.Span.BinarySearch(replyId) >= 0;
    }

    /// <summary>Gets whether a reply confirms an entry a keeper asks about.</summary>
    /// <param name="replyId">The reply, by id.</param>
    /// <returns>Whether it is one.</returns>
    public bool IsConfirmation(int replyId)
    {
        return Confirmations.Span.BinarySearch(replyId) >= 0;
    }

    /// <summary>Gets whether a reply leaves a dungeon.</summary>
    /// <param name="replyId">The reply, by id.</param>
    /// <returns>Whether it is one.</returns>
    public bool IsExitReply(int replyId)
    {
        return ExitReplies.Span.BinarySearch(replyId) >= 0;
    }
}
