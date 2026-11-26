using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Fighting;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Fighting;

public sealed class FightingModuleTests
{
    private static ActivityDefinition Fight()
    {
        var services = new ServiceCollection();

        FightingModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheHuntHasTheFourCategoriesOfAProfileInTheirOrder()
    {
        var sections = Fight().Settings.All.Select(setting => setting.Section?.In(CultureInfo.InvariantCulture)).Distinct().ToList();

        Assert.Equal(["What", "Where", "Bank", "Care and fights"], sections);
    }

    [Fact]
    public void TheRareSettingsOfTheHuntAreMarkedAdvancedAndTheOthersAreInView()
    {
        var settings = Fight().Settings.All;

        Assert.Equal(
            ["AfterLostFight", "Excluded", "Food", "FullAt", "IsolatedMaps", "LeastMonsters", "MostMonsters", "OwnBanking", "RestartAfterFailure", "Rotation", "UseZaaps", "ZaapSaving"],
            [.. settings.Where(setting => setting.IsAdvanced).Select(setting => setting.Key).Order()]);
        Assert.Equal(
            ["Area", "CaptureDragodindes", "CaptureMounts", "CaptureMuldos", "CaptureSouls", "CaptureVolkornes", "ChallengeReward", "Challenges", "Deposit", "Families", "FarmAnomalyFragments", "GoOnFull", "Heal", "HealBelow", "Mode", "Monsters", "Mule", "Place", "SubAreas", "Zone"],
            [.. settings.Where(setting => !setting.IsAdvanced).Select(setting => setting.Key).Order()]);
    }
}
