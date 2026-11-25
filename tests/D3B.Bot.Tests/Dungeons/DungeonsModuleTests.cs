using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Dungeons;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Dungeons;

public sealed class DungeonsModuleTests
{
    private static ActivityDefinition Dungeons()
    {
        var services = new ServiceCollection();

        DungeonsModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheModuleRegistersTheDungeonsWithTheirListOfDungeons()
    {
        var dungeons = Dungeons();
        var listed = Assert.Single(dungeons.Settings.All, setting => setting.Key == nameof(DungeonSettings.Dungeons));

        Assert.Equal(DungeonsModule.ActivityId, dungeons.Id);
        Assert.Equal((SettingKind.Dungeon, true), (listed.Kind, listed.IsList));
        Assert.Equal("Donjons", dungeons.Name.In(CultureInfo.GetCultureInfo("fr")));
    }

    [Fact]
    public void AProfileThatListsNoDungeonBreaksItsRule()
    {
        var settings = Dungeons().Settings;

        Assert.NotEmpty(settings.Check(new DungeonSettings()));
        Assert.Empty(settings.Check(new DungeonSettings { Dungeons = [1] }));
    }
}
