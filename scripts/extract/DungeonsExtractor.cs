using D3B.Data.Dungeons;
using D3B.Data.Texts;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the client's dungeons, the sub-areas that name each, and the replies of their keepers and exits.</summary>
/// <remarks>
/// <para>
/// The dungeon table is the only place the data says where a dungeon's door is (client.md). A keeper's replies are its own ids,
/// one text each: no id and no text id is shared from keeper to keeper, so they are found by their French words, as the
/// bankers' are (D80). A keeper is an NPC offering a reply that gives a key or uses the keyring; its confirmation is its
/// "Oui."; an exit reads "Sortir" or "Quitter". Two exits measured in the captures read otherwise and are kept by id,
/// when the client holds them (D522).
/// </para>
/// <para>
/// The extraction fails when the table is empty, a dungeon names an entrance of no map, or no keeper is found, so that a
/// client changing them is noticed.
/// </para>
/// </remarks>
internal static class DungeonsExtractor
{
    private const string KeyringReply = "Utiliser le trousseau de clefs";
    private const string Confirmation = "Oui.";

    // Measured on the owner's captures of 21 and 24 November 2026: the exits of Château Ensablé and of the Bworks.
    private static readonly int[] _measuredExits = [2_945, 55_364];

    private static readonly string[] _tables = ["dungeons", "subareas", "npcs"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static DungeonCatalog Read(ClientInstall client, IReadOnlySet<int> held)
    {
        try
        {
            var subAreas = SubAreasByDungeon(client);
            var dungeons = new List<Dungeon>();
            var rooms = new List<long>();
            var objects = new List<DungeonObject>();
            var bosses = new List<int>();
            var named = new List<int>();

            foreach (var record in UnityDataTable.ReadById(client, "dungeons").OrderBy(record => record.Int32("id")))
            {
                var id = record.Int32("id");
                var nameId = record.Int32("nameId");

                if (!held.Contains(nameId))
                    throw new InvalidDataException($"Dungeon {id} names text {nameId}, which a language lacks.");

                var entrance = record.Int32("entranceMapId");
                var exit = record.Int32("exitMapId");

                if (entrance <= 0 || exit <= 0)
                    throw new InvalidDataException($"Dungeon {id} has no entrance or no exit: {entrance}, {exit}.");

                var maps = record.Int32Array("mapIds");
                var required = record.Items("requiredObjects").Select(item => new DungeonObject(item.Int32("id"), item.Int32("quantity"))).ToArray();
                var own = record.Int32Array("bosses");
                var areas = subAreas.TryGetValue(id, out var found) ? found : [];

                dungeons.Add(new Dungeon(
                    id,
                    nameId,
                    record.Int32("minLevel"),
                    record.Int32("optimalPlayerLevel"),
                    entrance,
                    exit,
                    record.Byte("availableOnKeyring") != 0,
                    rooms.Count,
                    maps.Length,
                    objects.Count,
                    required.Length,
                    bosses.Count,
                    own.Length,
                    named.Count,
                    areas.Count));
                rooms.AddRange(maps.Select(map => (long)map));
                objects.AddRange(required);
                bosses.AddRange(own);
                named.AddRange(areas);
            }

            if (dungeons.Count is 0)
                throw new InvalidDataException("The client's table of dungeons is empty.");

            var (keys, keyrings, confirmations, exits) = ReadReplies(client);
            var catalog = new DungeonCatalog(dungeons.ToArray(), rooms.ToArray(), objects.ToArray(), bosses.ToArray(), named.ToArray(), keys, keyrings, confirmations, exits);

            Console.WriteLine($"Dungeons: {dungeons.Count:N0}, {dungeons.Count(dungeon => dungeon.ObjectCount > 0):N0} with a key, {rooms.Count:N0} rooms; keepers' replies {keys.Length:N0} with a key, {keyrings.Length:N0} with the keyring, {confirmations.Length:N0} confirmations; {exits.Length:N0} exits");

            return catalog;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"The dungeons do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static Dictionary<int, List<int>> SubAreasByDungeon(ClientInstall client)
    {
        var byDungeon = new Dictionary<int, List<int>>();

        foreach (var record in UnityDataTable.ReadById(client, "subareas").OrderBy(record => record.Int32("id")))
        {
            var dungeon = record.Int32("dungeonId");

            if (dungeon <= 0)
                continue;

            if (!byDungeon.TryGetValue(dungeon, out var areas))
                byDungeon[dungeon] = areas = [];

            areas.Add(record.Int32("id"));
        }

        return byDungeon;
    }

    private static (int[] Keys, int[] Keyrings, int[] Confirmations, int[] Exits) ReadReplies(ClientInstall client)
    {
        var french = TextsExtractor.Open(client, TextLanguages.French);
        var offsets = new Dictionary<int, int>(french.Texts.Count);

        foreach (var (id, offset) in french.Texts)
            offsets.TryAdd(id, offset);

        var keys = new SortedSet<int>();
        var keyrings = new SortedSet<int>();
        var confirmations = new SortedSet<int>();
        var exits = new SortedSet<int>();
        var known = new HashSet<int>();

        foreach (var npc in UnityDataTable.ReadById(client, "npcs"))
        {
            var id = npc.Int32("id");
            var yes = new List<int>();
            var keeper = false;

            foreach (var reply in npc.Items("dialogReplies"))
            {
                var values = reply.Int32Array("values");

                if (values.Length is not 2)
                    throw new InvalidDataException($"Non-player character {id} lists a reply as {values.Length} numbers, where the extractor expects a reply and its text.");

                known.Add(values[0]);

                if (!offsets.TryGetValue(values[1], out var offset))
                    continue;

                var text = french.TextAt(offset).Trim();

                if (IsKeyReply(text))
                {
                    keys.Add(values[0]);
                    keeper = true;
                }
                else if (text.StartsWith(KeyringReply, StringComparison.Ordinal))
                {
                    keyrings.Add(values[0]);
                    keeper = true;
                }
                else if (text == Confirmation)
                {
                    yes.Add(values[0]);
                }
                else if (text.StartsWith("Sortir", StringComparison.Ordinal) || text.StartsWith("Quitter", StringComparison.Ordinal))
                {
                    exits.Add(values[0]);
                }
            }

            // A "Oui." confirms an entry only from a keeper: other NPCs' answer their own questions.
            if (keeper)
                confirmations.UnionWith(yes);
        }

        exits.UnionWith(_measuredExits.Where(known.Contains));

        if (keys.Count is 0 || confirmations.Count is 0)
            throw new InvalidDataException("No non-player character gives a key and confirms it: the client's keepers read otherwise.");

        return ([.. keys], [.. keyrings], [.. confirmations], [.. exits]);
    }

    // "Donner la clef et entrer." and the keepers' other ways of giving it, all of which begin so.
    private static bool IsKeyReply(string text)
    {
        return text.StartsWith("Donner ", StringComparison.Ordinal) && text.Contains("clef", StringComparison.OrdinalIgnoreCase);
    }
}
