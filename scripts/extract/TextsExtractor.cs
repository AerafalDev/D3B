using D3B.Data.Packs;
using D3B.Data.Texts;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Extracts the numbered texts of each language of <see cref="TextLanguages.All"/> from <c>Content/I18n</c>.</summary>
/// <remarks>
/// Game data names texts by number. The keyed texts, found by a hash of a key the client builds at run time, belong to
/// its interface and are left out.
/// </remarks>
internal static class TextsExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. TextLanguages.All.Select(language => PathOf(client, language))];
    }

    public static void Extract(ClientInstall client, PackWriter writer)
    {
        foreach (var language in TextLanguages.All)
        {
            var path = PathOf(client, language);

            if (!File.Exists(path))
                throw new FileNotFoundException($"The client has no {language} texts: {path} does not exist.", path);

            var file = I18nFile.Read(path);
            var entries = file.Texts.OrderBy(text => text.Id).ToList();
            var ids = new int[entries.Count];
            var texts = new string[entries.Count];

            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0 && entries[i].Id == entries[i - 1].Id)
                    throw new InvalidDataException($"{path} holds text {entries[i].Id} twice.");

                ids[i] = entries[i].Id;
                texts[i] = file.TextAt(entries[i].Offset);
            }

            writer.Add(TextTable.EntryName(language), new TextTable(language, ids, texts));
            Console.WriteLine($"Texts ({language}): {ids.Length:N0}");
        }
    }

    private static string PathOf(ClientInstall client, string language)
    {
        return Path.Combine(client.StreamingAssets, "Content", "I18n", $"{language}.bin");
    }
}
