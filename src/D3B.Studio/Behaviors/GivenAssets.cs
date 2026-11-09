using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Studio.Assets;

namespace D3B.Studio.Behaviors;

/// <summary>The behaviors and profiles given with the app, read only: one behavior per activity, one profile per preset (D296, D303).</summary>
/// <remarks>
/// <para>
/// Made from the modules as the host starts, so that they follow the activities' settings as these change. Each activity's
/// behavior plays it whole, its node's every setting a public setting of the same key with the activity's default: a
/// profile of it changes what an activity's profile did (D297, D300). Its presets become its profiles, each holding only
/// what it changes from the defaults (D168, D304).
/// </para>
/// <para>Their ids are D3B's, never another's: <c>d3b.harvest</c>, <c>d3b.harvest.lumberjack</c>.</para>
/// </remarks>
public static class GivenAssets
{
    /// <summary>What every given asset's id starts with.</summary>
    public const string Prefix = "d3b.";

    private const string Main = "main";
    private const string Node = "activity";

    /// <summary>Gets the id of the behavior given for an activity.</summary>
    /// <param name="activityId">The activity's id.</param>
    /// <returns>The behavior's id.</returns>
    public static string BehaviorOf(string activityId)
    {
        ArgumentNullException.ThrowIfNull(activityId);

        return Prefix + activityId;
    }

    /// <summary>Makes the given assets.</summary>
    /// <param name="activities">The activities the modules registered.</param>
    /// <returns>A behavior per activity, then its profiles.</returns>
    public static IReadOnlyList<Asset> Of(ActivityCatalog activities)
    {
        ArgumentNullException.ThrowIfNull(activities);

        var assets = new List<Asset>();

        foreach (var activity in activities.All)
        {
            var settings = activity.Settings;
            var defaults = JsonNode.Parse(settings.Write(settings.CreateDefault())) as JsonObject ?? [];
            var behavior = new BehaviorAsset
            {
                Id = BehaviorOf(activity.Id),
                Name = activity.Name.In(CultureInfo.InvariantCulture),
                Title = activity.Name,
                Parameters = [.. settings.All.Select(setting => new BehaviorParameter(setting.Key, defaults[setting.Key]?.DeepClone()))],
                States =
                [
                    new BehaviorState(Main, activity.Name.In(CultureInfo.CurrentUICulture))
                    {
                        Actions = new ActionGraph(
                            Node,
                            [new GraphNode(Node, activity.Id, settings.All.ToImmutableSortedDictionary(setting => setting.Key, setting => (JsonNode?)new JsonObject { ["$param"] = setting.Key }, StringComparer.Ordinal))],
                            []),
                    },
                ],
            };

            assets.Add(behavior);

            foreach (var preset in settings.Presets)
            {
                var values = JsonNode.Parse(preset.Json) as JsonObject ?? [];

                assets.Add(new ProfileAsset
                {
                    Id = $"{behavior.Id}.{preset.Key}",
                    Name = preset.Name.In(CultureInfo.InvariantCulture),
                    Title = preset.Name,
                    Behavior = behavior.Id,
                    Values = values
                        .Where(value => !JsonNode.DeepEquals(value.Value, defaults[value.Key]))
                        .ToImmutableSortedDictionary(value => value.Key, value => value.Value?.DeepClone(), StringComparer.Ordinal),
                });
            }
        }

        return assets;
    }
}
