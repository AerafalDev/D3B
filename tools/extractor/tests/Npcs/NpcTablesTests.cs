using D3B.Extractor.Npcs;
using D3B.Extractor.Tests.Unity;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Npcs;

public sealed class NpcTablesTests
{
    private static readonly Text _banker = new() { Fr = "Banquier", En = "Banker", De = "Bankier", Es = "Banquero", Pt = "Banqueiro" };
    private static readonly Text _guard = new() { Fr = "Garde", En = "Guard", De = "Wache", Es = "Guardia", Pt = "Guarda" };
    private static readonly Text _welcome = new() { Fr = "Bienvenue, #1.", En = "Welcome, #1.", De = "Willkommen, #1.", Es = "Bienvenido, #1.", Pt = "Bem-vindo, #1." };
    private static readonly Text _chest = new() { Fr = "Consulter son coffre personnel.", En = "Open your chest.", De = "Truhe öffnen.", Es = "Abrir el cofre.", Pt = "Abrir o baú." };
    private static readonly Text _bye = new() { Fr = "Au revoir.", En = "Goodbye.", De = "Auf Wiedersehen.", Es = "Adiós.", Pt = "Adeus." };
    private static readonly Text _talk = new() { Fr = "Parler", En = "Talk", De = "Sprechen", Es = "Hablar", Pt = "Falar" };
    private static readonly Text _examine = new() { Fr = "Examiner", En = "Examine", De = "Untersuchen", Es = "Examinar", Pt = "Examinar" };

    private static readonly Npc _bankerNpc = new()
    {
        Id = 7,
        Name = _banker,
        Actions = [3],
        Questions = [new NpcLine { Id = 21, Text = _welcome }],
        Replies = [new NpcLine { Id = 11, Text = _chest }, new NpcLine { Id = 12, Text = _bye }],
    };

    private static readonly Npc _guardNpc = new()
    {
        Id = 9,
        Name = _guard,
        Actions = [3, 17],
        Questions = [],
        Replies = [new NpcLine { Id = 12, Text = _bye }, new NpcLine { Id = 13, Text = null }],
    };

    private static readonly NpcAction[] _actions = [new() { Id = 3, RealId = 3, Name = _talk }, new() { Id = 17, RealId = 3, Name = _examine }];

    [Fact]
    public void NpcsAndTheirActionsAreWrittenAsTheAppReadsThem()
    {
        // The app's own tests read the same fixtures (src-tauri/src/data/npcs/).
        Assert.Equal(Fixture("npcs.json"), NpcTables.WriteNpcs([_bankerNpc, _guardNpc]));
        Assert.Equal(Fixture("npc-actions.json"), NpcTables.WriteActions(_actions));
    }

    [Fact]
    public void AnNpcIsReadFieldByFieldWithItsQuestionsRepliesAndActions()
    {
        var texts = new Dictionary<long, Text> { [501] = _banker, [601] = _welcome, [701] = _chest, [702] = _bye };
        var npc = Assert.Single(NpcTables.ReadNpcs([NpcRecord(7, 501, [[21, 601]], [[11, 701], [12, 702]], [3])], id => texts[id], texts.GetValueOrDefault));

        Assert.Equal((7, "Banquier", 3), (npc.Id, npc.Name.Fr, Assert.Single(npc.Actions)));
        Assert.Equal((21, "Bienvenue, #1."), (Assert.Single(npc.Questions).Id, npc.Questions[0].Text?.Fr));
        Assert.Equal([11, 12], npc.Replies.Select(reply => reply.Id));
        Assert.Equal("Au revoir.", npc.Replies[1].Text?.Fr);
    }

    [Fact]
    public void ALineWhoseTextALanguageLacksKeepsItsIdWithoutText()
    {
        // 190 replies and 112 questions on 3.7.14.18, kept by the owner's choice of 2026-11-23.
        var line = Assert.Single(NpcTables.Lines(9, "dialogReplies", [[13, 703]], [], _ => null));

        Assert.Equal(13, line.Id);
        Assert.Null(line.Text);
    }

    [Fact]
    public void AnNpcWithoutANameIsRefused()
    {
        var failure = Assert.Throws<InvalidDataException>(() => NpcTables.ReadNpcs([NpcRecord(7, 0, [], [], [3])], _ => null, _ => null));

        Assert.Equal("NPC 7 names text 0.", failure.Message);
    }

    [Fact]
    public void ALineThatIsNotAPairOrWhoseIdReadsTwoTextsIsRefused()
    {
        var three = Assert.Throws<InvalidDataException>(() => NpcTables.Lines(7, "dialogReplies", [[11, 701, 1]], [], _ => null));
        var twice = Assert.Throws<InvalidDataException>(() => NpcTables.Lines(9, "dialogReplies", [[12, 799]], new Dictionary<int, int> { [12] = 702 }, _ => null));

        Assert.Equal("NPC 7 lists a pair of dialogReplies as 3 numbers, where the extractor expects an id and its text.", three.Message);
        Assert.Equal("NPC 9 reads 12 of dialogReplies as text 799, where another NPC reads it as 702.", twice.Message);
    }

    [Fact]
    public void AQuestionAndAReplyMayShareAnIdWithTwoTexts()
    {
        // Questions and replies are numbered apart: the server names each in its own field.
        var npcs = NpcTables.ReadNpcs([NpcRecord(7, 501, [[12, 601]], [[12, 702]], [3])], _ => _banker, _ => _bye);

        Assert.Equal((12, 12), (npcs[0].Questions[0].Id, npcs[0].Replies[0].Id));
    }

    [Fact]
    public void AnActionIsReadFieldByFieldAndRefusedWithoutAName()
    {
        var tree = TypeTree.Build("NpcActionData", [Node(0, "NpcActionData", "Base"), Node(1, "int", "id", 4), Node(1, "int", "realId", 4), Node(1, "int", "nameId", 4)]);
        var bytes = new ObjectBytes();

        bytes.Int32(17);
        bytes.Int32(3);
        bytes.Int32(482);

        var data = bytes.ToArray();
        var reader = new ObjectReader(data, tree);
        var record = Fields.Read(ref reader, 0, tree.Name);
        var action = Assert.Single(NpcTables.ReadActions([record], id => id == 482 ? _examine : null));

        Assert.Equal((17, 3, "Examiner"), (action.Id, action.RealId, action.Name.Fr));
        Assert.Throws<InvalidDataException>(() => NpcTables.ReadActions([record], _ => null));
    }

    [Fact]
    public void TablesWhoseReferencesHoldAreKeptWhole()
    {
        Assert.Equal([7, 9], NpcTables.Checked([_bankerNpc, _guardNpc], _actions).Select(npc => npc.Id));
    }

    [Fact]
    public void AnActionTheClientLacksOrAnIdHeldTwiceIsRefused()
    {
        var action = Assert.Throws<InvalidDataException>(() => NpcTables.Checked([_guardNpc], [_actions[0]]));

        Assert.Equal("NPC 9 offers action 17, which the client lacks.", action.Message);
        Assert.Throws<InvalidDataException>(() => NpcTables.Checked([_bankerNpc, _bankerNpc], _actions));
        Assert.Throws<InvalidDataException>(() => NpcTables.Checked([_bankerNpc], [.. _actions, _actions[0]]));
    }

    [Fact]
    public void TheInstalledClientsNpcsAreAsMeasured()
    {
        var client = Installed.Client("3.7.14.18");
        var texts = TextFiles.Open(client);
        var actions = NpcTables.ReadActions(DataTable.ReadById(client, "npcactions"), texts.Name);
        var npcs = NpcTables.Checked(NpcTables.ReadNpcs(DataTable.ReadById(client, "npcs"), texts.Name, texts.Find), actions);
        var questions = npcs.SelectMany(npc => npc.Questions).ToList();
        var replies = npcs.SelectMany(npc => npc.Replies).ToList();

        // 3.7.14.18, measured for format 6 (docs/data.md): 6,487 NPCs, 47,942 questions of which 112 without a text in
        // every language, 64,634 replies of which 190, 16 actions; 8 NPCs offer a reply reading the chest's text.
        Assert.Equal((6487, 16), (npcs.Count, actions.Count));
        Assert.Equal((47942, 112), (questions.Count, questions.Count(line => line.Text is null)));
        Assert.Equal((64634, 190), (replies.Count, replies.Count(line => line.Text is null)));
        Assert.Equal(8, npcs.Count(npc => npc.Replies.Any(reply => reply.Text?.Fr.StartsWith("Consulter son coffre personnel", StringComparison.Ordinal) is true)));
    }

    // An NPC's record as the client lays it out (3.7.14.18), its look, animations and dialogData left out.
    private static Fields NpcRecord(int id, int nameId, int[][] questions, int[][] replies, int[] actions)
    {
        var tree = TypeTree.Build("NpcData",
        [
            Node(0, "NpcData", "Base"),
            Node(1, "int", "id", 4),
            Node(1, "int", "nameId", 4),
            .. Pairs("dialogMessages"),
            .. Pairs("dialogReplies"),
            .. Vector(1, "actions", "int", 4),
        ]);
        var bytes = new ObjectBytes();

        bytes.Int32(id);
        bytes.Int32(nameId);

        foreach (var pairs in (ReadOnlySpan<int[][]>)[questions, replies])
        {
            bytes.Int32(pairs.Length);

            foreach (var pair in pairs)
            {
                Ints(bytes, pair);
            }
        }

        Ints(bytes, actions);

        var data = bytes.ToArray();
        var reader = new ObjectReader(data, tree);
        var fields = Fields.Read(ref reader, 0, tree.Name);

        reader.ExpectEnd();

        return fields;
    }

    private static IEnumerable<FlatNode> Pairs(string name)
    {
        return [Node(1, "vector", name), Array(2), Node(3, "int", "size", 4), Node(3, "NpcDialogPair", "data"), .. Vector(4, "values", "int", 4)];
    }

    private static void Ints(ObjectBytes bytes, int[] values)
    {
        bytes.Int32(values.Length);

        foreach (var value in values)
        {
            bytes.Int32(value);
        }
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "npcs", name));
    }
}
