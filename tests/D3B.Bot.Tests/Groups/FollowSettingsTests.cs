using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Groups;

public sealed class FollowSettingsTests
{
    private static ActivityDefinition Definition()
    {
        var services = new ServiceCollection();

        GroupsModule.Register(services);

        using var provider = services.BuildServiceProvider();

        return Assert.Single(provider.GetServices<ActivityDefinition>());
    }

    [Fact]
    public void ACharacterIsNamedByItsAccountAndItsIdAndReadBack()
    {
        var key = new CharacterKey(7, 70_001);

        Assert.Equal("7:70001", key.ToId());
        Assert.True(CharacterKey.TryParseId(key.ToId(), out var read));
        Assert.Equal(key, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("70001")]
    [InlineData("7:70001:1")]
    [InlineData("seven:70001")]
    [InlineData("-7:70001")]
    [InlineData("7: 70001")]
    public void ATextNamingNoCharacterIsNotRead(string? id)
    {
        Assert.False(CharacterKey.TryParseId(id, out _));
    }

    [Fact]
    public void TheFollowActivityAsksForItsMasterACharacter()
    {
        var definition = Definition();
        var master = definition.Settings.All[0];

        Assert.Equal(GroupsModule.ActivityId, definition.Id);
        Assert.Equal(nameof(FollowSettings.Master), master.Key);
        Assert.Equal(SettingKind.Character, master.Kind);

        // No master: required, and the rule says to choose one.
        Assert.Contains(definition.Settings.Check(new FollowSettings()), problem => problem.Kind is SettingProblemKind.Required);
        Assert.Contains(definition.Settings.Check(new FollowSettings()), problem => problem.Kind is SettingProblemKind.Rule);

        // A text naming no character is unknown; one naming a character holds.
        Assert.Contains(definition.Settings.Check(new FollowSettings { Master = "nobody" }), problem => problem.Kind is SettingProblemKind.Unknown);
        Assert.Empty(definition.Settings.Check(new FollowSettings { Master = "7:70001" }));
    }

    [Fact]
    public async Task AFollowerMayFightAsAMule()
    {
        // The owner's choice (D579): a flag of the profile, off by default.
        await using var play = new TestPlay();
        var mule = Assert.Single(Definition().Settings.All, setting => setting.Key == nameof(FollowSettings.Mule));

        Assert.Equal(SettingKind.Flag, mule.Kind);
        Assert.True(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId(), Mule = true }, play.Bots, play.Tables).FightsAsMule);
        Assert.False(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables).FightsAsMule);
    }

    [Fact]
    public void AFollowersMasterIsStoredAndReadBack()
    {
        var settings = Definition().Settings;
        var stored = settings.Write(new FollowSettings { Master = "7:70001" });
        var reading = settings.Read(stored);

        Assert.Empty(reading.Reset);
        Assert.Equal("7:70001", Assert.IsType<FollowSettings>(reading.Settings).Master);
    }

    [Fact]
    public void AFollowerOfNoMasterSaysSo()
    {
        var failure = Assert.Throws<ActivityException>(() => FollowActivity.MasterOf(new FollowSettings()));

        Assert.Equal("No master is chosen.", TestGroup.Say(failure.Reason));
        Assert.Equal(new CharacterKey(7, 70_001), FollowActivity.MasterOf(new FollowSettings { Master = "7:70001" }));
    }
}
