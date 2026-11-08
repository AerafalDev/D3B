using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

public sealed class GivenAssetsTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    [Fact]
    public void EachActivityIsABehaviorPlayingItWholeAndEachPresetAProfileOfWhatItChanges()
    {
        var assets = GivenAssets.Of(new ActivityCatalog([SettingsDefinition()]));
        var behavior = Assert.IsType<BehaviorAsset>(assets[0]);
        var profile = Assert.IsType<ProfileAsset>(Assert.Single(assets.Skip(1)));

        Assert.Equal(("d3b.test.settings", true), (behavior.Id, behavior.IsGiven));
        Assert.Equal(["Walks", "Job", "Resources"], behavior.Parameters.Select(parameter => parameter.Key));
        Assert.Equal(10, behavior.Parameters[0].Value!.GetValue<int>());
        Assert.Equal(("d3b.test.settings.seven", "Seven walks", behavior.Id), (profile.Id, profile.Title!.In(CultureInfo.InvariantCulture), profile.Behavior));
        Assert.Equal(["Walks"], profile.Values.Keys);
    }

    [Fact]
    public async Task EveryGivenBehaviorCompilesWithEachOfItsProfiles()
    {
        await _behaviors.PreparedAsync();

        foreach (var profile in _behaviors.Assets.OfType<ProfileAsset>())
            Assert.Empty(_behaviors.Starter.Compile(profile).Problems);

        Assert.Empty(_behaviors.Compile(_behaviors.Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(PlainActivityId))!).Problems);
    }

    [Fact]
    public async Task AProfileIsTheSettingsOfTheActivityItsBehaviorPlaysWholeAndKeepsOnlyWhatDiffers()
    {
        await _behaviors.PreparedAsync();

        var behavior = _behaviors.Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(SettingsActivityId))!;
        var activity = ProfileForms.ActivityOf(behavior, _behaviors.Nodes);

        Assert.Equal(SettingsActivityId, activity?.Id);

        var settings = activity!.Settings.Read(ProfileForms.SettingsOf(behavior, Values(("Walks", 4)))).Settings as TestSettings;

        Assert.Equal(4, settings!.Walks);

        var values = ProfileForms.ValuesOf(behavior, activity.Settings.Write(new TestSettings { Walks = 10, Job = TestReferences.Lumberjack }));

        Assert.Equal(["Job"], values.Keys);
    }

    [Fact]
    public void ABehaviorOfMoreThanOneActivityHasNoProfileForm()
    {
        var behavior = Behavior(
            new BehaviorState("a", "A") { Actions = Chain(Node("x", SettingsActivityId)) },
            new BehaviorState("b", "B") { Actions = Chain(Node("y", PlainActivityId)) });

        Assert.Null(ProfileForms.ActivityOf(behavior, _behaviors.Nodes));
    }
}
