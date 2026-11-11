namespace D3B.Extractor.Texts;

/// <summary>The client's texts in the five languages the data folder holds, looked up by the number a table names.</summary>
internal sealed class TextFiles
{
    /// <summary>The languages the data folder holds, in the order each record writes them (ADR 0008).</summary>
    public static readonly IReadOnlyList<string> Languages = ["fr", "en", "de", "es", "pt"];

    private readonly IReadOnlyList<I18nFile> _files;

    private TextFiles(IReadOnlyList<I18nFile> files)
    {
        _files = files;
    }

    /// <summary>Opens the text file of each language.</summary>
    /// <exception cref="InvalidDataException">The client lacks a language, or its file is not laid out as measured.</exception>
    public static TextFiles Open(ClientInstall client)
    {
        return From(Languages.Select(language =>
        {
            var path = Path.Combine(client.StreamingAssets, "Content", "I18n", $"{language}.bin");

            return File.Exists(path) ? File.ReadAllBytes(path) : throw new InvalidDataException($"The client has no {language} texts: {path} does not exist.");
        }));
    }

    /// <summary>Reads the text files' bytes, one per language of <see cref="Languages"/> in its order.</summary>
    public static TextFiles From(IEnumerable<byte[]> files)
    {
        var read = files.Select(I18nFile.Read).ToList();

        if (read.Count != Languages.Count)
        {
            throw new ArgumentException($"{read.Count} text files for {Languages.Count} languages.", nameof(files));
        }

        // Tables name text 0 for no name; a file that holds one would make those records named, not nameless.
        if (read.FindIndex(file => file.HoldsTextZero) is var index and >= 0)
        {
            throw new InvalidDataException($"The {Languages[index]} texts hold a text 0, which the extractor reads as no text.");
        }

        return new TextFiles(read);
    }

    /// <summary>Gets the text a table names, or <see langword="null"/> for text 0, which names none.</summary>
    /// <exception cref="InvalidDataException">A language lacks the text.</exception>
    public Text? Name(long id)
    {
        // Thousands of maps name text 0, which no file holds: they have no name (main:docs/client.md, "Texts").
        if (id is 0)
        {
            return null;
        }

        var texts = new string[_files.Count];

        for (var i = 0; i < texts.Length; i++)
        {
            if (id is < int.MinValue or > int.MaxValue || !_files[i].TryGet((int)id, out texts[i]))
            {
                throw new InvalidDataException($"The {Languages[i]} texts lack text {id}, which a table names.");
            }
        }

        return new Text { Fr = texts[0], En = texts[1], De = texts[2], Es = texts[3], Pt = texts[4] };
    }
}
