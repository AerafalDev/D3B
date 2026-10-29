using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Settings;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Data.Texts;

namespace D3B.App.Tests;

/// <summary>The game's values of a small world, named in French and English by a pack's texts, as the app's pickers read them.</summary>
internal sealed class TestGameData : IDisposable
{
    private static readonly (int Id, string French, string English)[] _names =
    [
        (101, "Amakna", "Amakna"),
        (102, "Astrub", "Astrub"),
        (110, "Forêt d'Amakna", "Amakna Forest"),
        (111, "Collines d'Amakna", "Amakna Hills"),
        (120, "Cité d'Astrub", "Astrub City"),
        (202, "Bûcheron", "Lumberjack"),
        (224, "Mineur", "Miner"),
        (403, "Frêne", "Ash"),
        (404, "Châtaignier", "Chestnut"),
        (412, "Fer", "Iron"),
    ];

    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;

    public GameReferences References { get; } = TestReferences.Game();

    public GameTexts Texts { get; }

    public TestGameData()
    {
        var path = _directory.Combine("texts.pack");
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using (var stream = File.Create(path))
        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(TextTable.EntryName(TextLanguages.French), new TextTable(TextLanguages.French, [.. _names.Select(name => name.Id)], [.. _names.Select(name => name.French)]));
            writer.Add(TextTable.EntryName(TextLanguages.English), new TextTable(TextLanguages.English, [.. _names.Select(name => name.Id)], [.. _names.Select(name => name.English)]));
            writer.Complete();
        }

        _pack = GamePack.Open(path);
        Texts = new GameTexts(_pack);
    }

    public void Dispose()
    {
        _pack.Dispose();
        _directory.Dispose();
    }
}
