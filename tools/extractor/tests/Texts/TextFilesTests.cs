using System.Text;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Tests.Texts;

public sealed class TextFilesTests
{
    [Fact]
    public void ATextIsFoundByItsNumberInEachLanguage()
    {
        var texts = TextFiles.From(TextFiles.Languages.Select(language => File([(7, $"Astrub {language}")])));

        var name = texts.Name(7);

        Assert.NotNull(name);
        Assert.Equal(["Astrub fr", "Astrub en", "Astrub de", "Astrub es", "Astrub pt"], [name.Fr, name.En, name.De, name.Es, name.Pt]);
    }

    [Fact]
    public void TextZeroNamesNothing()
    {
        Assert.Null(TextFiles.From(TextFiles.Languages.Select(_ => File([]))).Name(0));
    }

    [Fact]
    public void ATextOneLanguageLacksFailsTheExtraction()
    {
        var texts = TextFiles.From(TextFiles.Languages.Select(language => File(language is "es" ? [] : [(7, "Astrub")])));

        var error = Assert.Throws<InvalidDataException>(() => texts.Name(7));

        Assert.Contains("es texts lack text 7", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextHeldTwiceIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => I18nFile.Read(File([(7, "a"), (7, "b")])));
    }

    [Fact]
    public void ACountLargerThanTheFileIsRefused()
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8);

        writer.Write("fr");
        writer.Write(1_000_000);
        writer.Flush();

        Assert.Throws<InvalidDataException>(() => I18nFile.Read(buffer.ToArray()));
    }

    [Fact]
    public void AnOffsetOutsideTheTextsIsRefused()
    {
        var data = File([(7, "Astrub")]);

        // The offset of text 7: after the language, the count and the identifier.
        BitConverter.GetBytes(2).CopyTo(data, 3 + 4 + 4);

        var file = I18nFile.Read(data);

        Assert.Throws<InvalidDataException>(() => file.TryGet(7, out _));
    }

    [Fact]
    public void TheInstalledClientNamesItsAreasInFiveLanguages()
    {
        var client = Installed.Client();
        var area = DataTable.ReadById(client, "areas")[0];
        var name = TextFiles.Open(client).Name(area.Integer("nameId"));

        Assert.NotNull(name);
        Assert.All([name.Fr, name.En, name.De, name.Es, name.Pt], text => Assert.NotEmpty(text));
    }

    /// <summary>A text file as the client lays it out, with its texts and one keyed text.</summary>
    private static byte[] File((int Id, string Text)[] texts)
    {
        var header = 3 + 4 + (texts.Length * 8) + 4 + 12;
        var bodies = texts.Select(text => Encoding.UTF8.GetBytes(text.Text)).ToList();

        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8);

        writer.Write("fr");
        writer.Write(texts.Length);

        var offset = header;

        for (var i = 0; i < texts.Length; i++)
        {
            writer.Write(texts[i].Id);
            writer.Write(offset);
            offset += 1 + bodies[i].Length;
        }

        writer.Write(1);
        writer.Write(0xDEADBEEFUL);
        writer.Write(header);

        foreach (var text in texts)
        {
            writer.Write(text.Text);
        }

        writer.Flush();

        return buffer.ToArray();
    }
}
