using D3B.Extractor.Items;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Npcs;

/// <summary>The non-player characters of the data folder and the actions taken on them: <c>npcs.json</c> and <c>npc-actions.json</c>.</summary>
/// <remarks>
/// <para>
/// An NPC keeps its name, its actions, and its dialog's questions and replies with their texts (the owner, 2026-11-23):
/// the bot finds the bankers and the reply that opens a bank from them, in Rust. The client says nothing of what a reply
/// does, only its text, and places no NPC on a map: the server does, with the map's information. Its look, its
/// animations and its <c>dialogData</c> (a question's bubble and mood) are left out.
/// </para>
/// <para>
/// A question and a reply are a pair, the id the server names it by and its text. One id reads one text whatever the NPC
/// (measured on 3.7.14.18): the extraction fails when it reads two, since a line would then not be known by its id.
/// </para>
/// </remarks>
internal static class NpcTables
{
    /// <summary>Reads the NPCs and their actions from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's NPCs are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, DataFolder folder, TextWriter output)
    {
        var actions = ReadActions(DataTable.ReadById(client, "npcactions"), texts.Name);
        var npcs = Checked(ReadNpcs(DataTable.ReadById(client, "npcs"), texts.Name, texts.Find), actions);

        folder.Write("npcs.json", WriteNpcs(npcs));
        folder.Write("npc-actions.json", WriteActions(actions));

        var questions = npcs.SelectMany(npc => npc.Questions).ToList();
        var replies = npcs.SelectMany(npc => npc.Replies).ToList();

        output.WriteLine($"NPCs: {npcs.Count:N0} NPCs, {questions.Count:N0} questions and {replies.Count:N0} replies, of which {questions.Count(line => line.Text is null):N0} and {replies.Count(line => line.Text is null):N0} without a text in every language; {actions.Count:N0} actions");
    }

    /// <param name="records">The client's <c>NpcData</c>.</param>
    /// <param name="name">The text of an NPC's name, which every language holds.</param>
    /// <param name="line">The text of a question or a reply, or <see langword="null"/> when a language lacks it.</param>
    /// <exception cref="InvalidDataException">An NPC names text 0, a line is not a pair, or an id reads two texts.</exception>
    public static IReadOnlyList<Npc> ReadNpcs(IEnumerable<Fields> records, Func<long, Text?> name, Func<long, Text?> line)
    {
        var questions = new Dictionary<int, int>();
        var replies = new Dictionary<int, int>();

        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Npc
                    {
                        Id = id,
                        Name = ItemTables.Named("NPC", id, name(record.Int32("nameId"))),
                        Actions = record.Int32Array("actions"),
                        Questions = Lines(id, "dialogMessages", [.. record.Items("dialogMessages").Select(pair => pair.Int32Array("values"))], questions, line),
                        Replies = Lines(id, "dialogReplies", [.. record.Items("dialogReplies").Select(pair => pair.Int32Array("values"))], replies, line),
                    };
                })
                .OrderBy(npc => npc.Id),
        ];
    }

    /// <summary>Reads an NPC's pairs of one field, each an id and its text, checking that each id reads the text <paramref name="known"/> holds for it.</summary>
    /// <exception cref="InvalidDataException">A pair is not two numbers, or its id reads another text than for another NPC.</exception>
    public static IReadOnlyList<NpcLine> Lines(int npc, string field, IReadOnlyList<int[]> pairs, Dictionary<int, int> known, Func<long, Text?> line)
    {
        var lines = new List<NpcLine>(pairs.Count);

        foreach (var pair in pairs)
        {
            if (pair.Length is not 2)
            {
                throw new InvalidDataException($"NPC {npc} lists a pair of {field} as {pair.Length} numbers, where the extractor expects an id and its text.");
            }

            if (known.TryGetValue(pair[0], out var text) && text != pair[1])
            {
                throw new InvalidDataException($"NPC {npc} reads {pair[0]} of {field} as text {pair[1]}, where another NPC reads it as {text}.");
            }

            known[pair[0]] = pair[1];
            lines.Add(new NpcLine { Id = pair[0], Text = line(pair[1]) });
        }

        return lines;
    }

    /// <exception cref="InvalidDataException">An action names text 0.</exception>
    public static IReadOnlyList<NpcAction> ReadActions(IEnumerable<Fields> records, Func<long, Text?> name)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new NpcAction { Id = id, RealId = record.Int32("realId"), Name = ItemTables.Named("NPC action", id, name(record.Int32("nameId"))) };
                })
                .OrderBy(action => action.Id),
        ];
    }

    /// <summary>Checks the references the app relies on.</summary>
    /// <exception cref="InvalidDataException">An id is held twice, or an NPC offers an action the client lacks.</exception>
    public static IReadOnlyList<Npc> Checked(IReadOnlyList<Npc> npcs, IReadOnlyList<NpcAction> actions)
    {
        ItemTables.Unique("NPCs", npcs, npc => npc.Id);

        var known = ItemTables.Unique("NPC actions", actions, action => action.Id);

        foreach (var npc in npcs)
        {
            foreach (var action in npc.Actions)
            {
                if (!known.Contains(action))
                {
                    throw new InvalidDataException($"NPC {npc.Id} offers action {action}, which the client lacks.");
                }
            }
        }

        return npcs;
    }

    public static byte[] WriteNpcs(IEnumerable<Npc> npcs)
    {
        return JsonTable.Write(npcs, (json, npc) =>
        {
            json.WriteNumber("id", npc.Id);
            JsonTable.WriteText(json, "name", npc.Name);
            json.WriteStartArray("actions");

            foreach (var action in npc.Actions)
            {
                json.WriteNumberValue(action);
            }

            json.WriteEndArray();

            foreach (var (field, lines) in (ReadOnlySpan<(string, IReadOnlyList<NpcLine>)>)[("questions", npc.Questions), ("replies", npc.Replies)])
            {
                json.WriteStartArray(field);

                foreach (var line in lines)
                {
                    json.WriteStartObject();
                    json.WriteNumber("id", line.Id);
                    JsonTable.WriteText(json, "text", line.Text);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            }
        });
    }

    public static byte[] WriteActions(IEnumerable<NpcAction> actions)
    {
        return JsonTable.Write(actions, (json, action) =>
        {
            json.WriteNumber("id", action.Id);
            json.WriteNumber("realId", action.RealId);
            JsonTable.WriteText(json, "name", action.Name);
        });
    }
}
