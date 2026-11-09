using D3B.Bot.Banks;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Tests.Banks;

public sealed class BankingPreferencesTests
{
    private static IConfiguration Of(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();
    }

    [Fact]
    public void ABotBanksAtNinetyPercentAndMayTakeAZaapUntilTheSettingsSayOtherwise()
    {
        var preferences = BankingPreferences.Read(Of());

        Assert.Equal(90, preferences.FullAt);
        Assert.True(preferences.UseZaaps);
    }

    [Fact]
    public void ThePreferencesReadBackFromWhatTheyWroteAndLeaveTheDefaultsUnset()
    {
        var preferences = BankingPreferences.Read(Of((BankingPreferences.FullAtKey, "98"), (BankingPreferences.UseZaapsKey, "false")));
        var written = preferences.ToConfiguration();

        Assert.Equal(98, preferences.FullAt);
        Assert.False(preferences.UseZaaps);
        Assert.Equal("98", written[BankingPreferences.FullAtKey]);
        Assert.Equal("False", written[BankingPreferences.UseZaapsKey]);
        Assert.All(new BankingPreferences().ToConfiguration().Values, Assert.Null);
    }

    [Theory]
    [InlineData("49")]
    [InlineData("101")]
    [InlineData("many")]
    public void AShareOutsideItsBoundsStopsTheHost(string share)
    {
        Assert.Throws<InvalidOperationException>(() => BankingPreferences.Read(Of((BankingPreferences.FullAtKey, share))));
    }

    [Fact]
    public void AValueThatIsNotABooleanStopsTheHost()
    {
        Assert.Throws<InvalidOperationException>(() => BankingPreferences.Read(Of((BankingPreferences.UseZaapsKey, "maybe"))));
    }

    [Fact]
    public void AShareSetOutsideItsBoundsIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BankingPreferences().FullAt = 101);
    }

    [Fact]
    public void AProfileThatAsksForItsOwnBankingIsNotAskedTheGeneralSettings()
    {
        var preferences = new BankingPreferences { FullAt = 98, UseZaaps = false };

        Assert.Equal(98, preferences.FullAtOf(false, 60));
        Assert.Equal(60, preferences.FullAtOf(true, 60));
        Assert.False(preferences.UseZaapsOf(false, true));
        Assert.True(preferences.UseZaapsOf(true, true));
    }
}
