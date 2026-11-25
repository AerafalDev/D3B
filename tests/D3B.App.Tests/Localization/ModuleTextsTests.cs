using System.Collections;
using System.Globalization;
using System.Resources;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Dungeons;
using D3B.Bot.Fighting;
using D3B.Bot.Groups;
using D3B.Bot.Harvesting;
using D3B.Bot.Transfers;
using D3B.Bot.Zaaps;
using D3B.Studio.Behaviors;

namespace D3B.App.Tests.Localization;

public sealed class ModuleTextsTests
{
    public static TheoryData<string> Modules { get; } = [nameof(ActivityTexts), nameof(DungeonTexts), nameof(FightTexts), nameof(GroupTexts), nameof(HarvestTexts), nameof(TransferTexts), nameof(ZaapTexts), nameof(BehaviorTexts)];

    private static ResourceManager Resources(string module)
    {
        return module switch
        {
            nameof(ActivityTexts) => ActivityTexts.Resources,
            nameof(DungeonTexts) => DungeonTexts.Resources,
            nameof(FightTexts) => FightTexts.Resources,
            nameof(GroupTexts) => GroupTexts.Resources,
            nameof(HarvestTexts) => HarvestTexts.Resources,
            nameof(TransferTexts) => TransferTexts.Resources,
            nameof(ZaapTexts) => ZaapTexts.Resources,
            _ => BehaviorTexts.Resources,
        };
    }

    private static SortedSet<string> Keys(ResourceManager resources, CultureInfo culture)
    {
        var set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);

        Assert.NotNull(set);

        return new SortedSet<string>(set.Cast<DictionaryEntry>().Select(entry => (string)entry.Key), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void EveryTextOfAModuleIsInEnglishFrenchAndSpanish(string module)
    {
        var resources = Resources(module);
        var english = Keys(resources, CultureInfo.InvariantCulture);

        Assert.NotEmpty(english);
        Assert.Equal(english, Keys(resources, Localizer.French));
        Assert.Equal(english, Keys(resources, Localizer.Spanish));
    }
}
