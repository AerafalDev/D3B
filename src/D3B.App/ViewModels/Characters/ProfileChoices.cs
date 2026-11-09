using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Characters;

/// <summary>The profiles a character may be given: the project's first, then those given with D3B (D296, D324).</summary>
internal static class ProfileChoices
{
    /// <summary>Lists the profiles.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="assets">The project's assets and those given, if the host has them.</param>
    /// <returns>The project's profiles, by name, then the given ones, in their order.</returns>
    public static (IReadOnlyList<ProfileChoice> Mine, IReadOnlyList<ProfileChoice> Given) Of(Localizer localizer, AssetLibrary? assets)
    {
        var profiles = assets?.OfType<ProfileAsset>().ToList() ?? [];

        return (
            [.. profiles.Where(profile => !Given(profile)).Select(profile => Choice(localizer, assets!, profile)).OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)],
            [.. profiles.Where(Given).Select(profile => Choice(localizer, assets!, profile))]);
    }

    /// <summary>Lists the activities, each played with its defaults for the session, as the bots' cards offered them (D99).</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="activities">The activities the modules registered, if the host has them.</param>
    /// <returns>The activities, in the catalogue's order.</returns>
    public static IReadOnlyList<ProfileChoice> Activities(Localizer localizer, ActivityCatalog? activities)
    {
        return [.. (activities?.All ?? []).Select(activity => Activity(localizer, activity))];
    }

    /// <summary>Describes an activity played with its defaults.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="activity">The activity.</param>
    /// <returns>Its choice.</returns>
    public static ProfileChoice Activity(Localizer localizer, ActivityDefinition activity)
    {
        return new ProfileChoice(null, activity.Name.In(localizer.Culture), localizer["DefaultSettings"], false, activity.Id);
    }

    /// <summary>Describes a profile.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="assets">The project's assets and those given.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>Its choice.</returns>
    public static ProfileChoice Choice(Localizer localizer, AssetLibrary assets, ProfileAsset profile)
    {
        var behavior = assets.Find<BehaviorAsset>(profile.Behavior);
        var behaviorName = behavior?.Title?.In(localizer.Culture) ?? behavior?.Name ?? localizer.Format("BehaviorMissing", profile.Behavior);
        var detail = Given(profile) ? localizer.Format("ProfileGivenDetail", behaviorName)
            : profile.Values.Count > 0 ? localizer.Format("ProfileChangedDetail", behaviorName, localizer.Plural("ValuesChanged", profile.Values.Count))
            : behaviorName;

        return new ProfileChoice(profile.Id, profile.Title?.In(localizer.Culture) ?? profile.Name, detail, Given(profile));
    }

    private static bool Given(ProfileAsset profile)
    {
        return profile.IsGiven;
    }
}
