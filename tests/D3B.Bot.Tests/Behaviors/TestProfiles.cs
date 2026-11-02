using D3B.Bot.Assets;
using D3B.Bot.Behaviors;

namespace D3B.Bot.Tests.Behaviors;

/// <summary>Saves a profile of an activity's given behavior from its settings, as the app's profile editor does (D300).</summary>
internal static class TestProfiles
{
    public static Task<ProfileAsset> SaveProfileAsync(this AssetLibrary assets, string name, string activityId, string settings, CancellationToken cancellationToken)
    {
        var behavior = assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(activityId)) ?? throw new InvalidOperationException($"No behavior is given for {activityId}.");

        return assets.SaveAsync(
            new ProfileAsset { Id = AssetLibrary.NewId(), Name = name, Behavior = behavior.Id, Values = ProfileForms.ValuesOf(behavior, settings) },
            cancellationToken);
    }
}
