using D3B.Data.Npcs;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the non-player characters, and the texts of the questions they ask and the replies they offer.</summary>
/// <remarks>
/// <para>
/// An NPC's <c>dialogMessages</c> and <c>dialogReplies</c> are pairs, an id and the text it reads (client.md). The server names a
/// question and a reply by that id alone, which holds for every NPC: the extraction fails when an id has two texts, so that a
/// client changing it is noticed. A pair whose text a language lacks is left out, and the studio names it by its id.
/// </para>
/// </remarks>
internal static class NpcsExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "npcs")];
    }

    public static NpcCatalog Read(ClientInstall client, IReadOnlySet<int> held)
    {
        var npcs = new List<Npc>();
        var replies = new SortedDictionary<int, int>();
        var questions = new SortedDictionary<int, int>();
        var left = 0;

        foreach (var npc in UnityDataTable.ReadById(client, "npcs").OrderBy(record => record.Int32("id")))
        {
            var id = npc.Int32("id");

            npcs.Add(new Npc(id, npc.Int32("nameId")));
            left += Collect(npc, "dialogReplies", id, held, replies);
            left += Collect(npc, "dialogMessages", id, held, questions);
        }

        if (npcs.Count is 0)
            throw new InvalidDataException("The client's non-player character table is empty.");

        var catalog = new NpcCatalog(
            npcs.ToArray(),
            replies.Select(pair => new NpcText(pair.Key, pair.Value)).ToArray(),
            questions.Select(pair => new NpcText(pair.Key, pair.Value)).ToArray());

        Console.WriteLine($"NPCs: {npcs.Count:N0}, {replies.Count:N0} replies and {questions.Count:N0} questions with their texts, {left:N0} pairs left out for a text a language lacks");

        return catalog;
    }

    private static int Collect(UnityFields npc, string field, int npcId, IReadOnlySet<int> held, SortedDictionary<int, int> texts)
    {
        var left = 0;

        foreach (var pair in npc.Items(field))
        {
            var values = pair.Int32Array("values");

            if (values.Length is not 2)
                throw new InvalidDataException($"Non-player character {npcId} lists a pair of {field} as {values.Length} numbers, where the extractor expects an id and its text.");

            if (!held.Contains(values[1]))
            {
                left++;

                continue;
            }

            if (texts.TryGetValue(values[0], out var known) && known != values[1])
                throw new InvalidDataException($"Non-player character {npcId} reads {values[0]} of {field} as text {values[1]}, where another read it as {known}: the ids are not the same for every NPC.");

            texts[values[0]] = values[1];
        }

        return left;
    }
}
