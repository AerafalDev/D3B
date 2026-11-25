using D3B.Data.Texts;

namespace D3B.Data.Tests.Texts;

public sealed class TextLanguagesTests
{
    [Fact]
    public void APackHoldsTheTextsInFrenchEnglishAndSpanish()
    {
        Assert.Equal([TextLanguages.French, TextLanguages.English, TextLanguages.Spanish], TextLanguages.All);
    }

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("en", "en")]
    [InlineData("es", "es")]
    [InlineData("ES", "es")]
    [InlineData("de", "en")]
    [InlineData("", "en")]
    public void TheAppsLanguageTakesThePacksTextsInItElseInEnglish(string app, string expected)
    {
        Assert.Equal(expected, TextLanguages.Of(app));
    }
}
