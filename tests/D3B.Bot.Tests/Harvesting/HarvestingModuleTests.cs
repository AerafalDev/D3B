using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestingModuleTests
{
    private static ActivityDefinition Harvest()
    {
        var services = new ServiceCollection();

        HarvestingModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheOwnersListProfileOfBeforeReadsAsAListWithoutAnythingToRedo()
    {
        var settings = Harvest().Settings;

        // Stored by 6d: two resources, Amakna, the guild's chest.
        var reading = settings.Read("""{"Resources":[312,441],"EverythingKnown":false,"Area":0,"SubAreas":[],"IsolatedMaps":false,"FullAt":50,"Deposit":"Harvest","DepositTo":"GuildChest","OpenBags":true,"Heal":true,"HealBelow":80,"Food":[528],"AfterFight":"Resume"}""");
        var read = Assert.IsType<HarvestSettings>(reading.Settings);

        Assert.Empty(reading.Reset);
        Assert.Equal(HarvestMode.List, read.Mode);
        Assert.Equal([312, 441], read.Resources);
        Assert.Equal(HarvestPlace.Chosen, read.Place);
        Assert.Equal(0, read.Area);
        Assert.Equal(HarvestDepositPlace.GuildChest, read.DepositTo);
        Assert.Empty(settings.Check(read));
    }

    [Fact]
    public void AZonePlaceIsStoredWithItsZoneNeedsAZoneAndLeavesAProfileOfBeforeAsItWas()
    {
        var settings = Harvest().Settings;
        var before = Assert.IsType<HarvestSettings>(settings.Read("""{"Resources":[312],"Place":"Chosen","SubAreas":[7]}""").Settings);
        var zone = Assert.IsType<HarvestSettings>(settings.Read("""{"Resources":[312],"Place":"Zone","Zone":"woods"}""").Settings);

        Assert.Equal((HarvestPlace.Chosen, ""), (before.Place, before.Zone));
        Assert.Equal([7], before.SubAreas);
        Assert.Equal((HarvestPlace.Zone, "woods"), (zone.Place, zone.Zone));
        Assert.Contains("\"Zone\":\"woods\"", settings.Write(zone).Replace(" ", ""));
        Assert.Empty(settings.Check(zone));
        Assert.Single(settings.Check(new HarvestSettings { Resources = [312], Place = HarvestPlace.Zone }));
        Assert.Empty(settings.Check(before));
    }

    [Fact]
    public void AProfileThatStoredItsShareOfPodsKeepsBankingByItAndANewOneFollowsTheGeneralSettings()
    {
        var settings = Harvest().Settings;
        var before = Assert.IsType<HarvestSettings>(settings.Read("""{"Resources":[312],"FullAt":75,"UseZaaps":true}""").Settings);
        var made = Assert.IsType<HarvestSettings>(settings.Read("""{"Resources":[312]}""").Settings);
        var chosen = Assert.IsType<HarvestSettings>(settings.Read("""{"Resources":[312],"FullAt":75,"OwnBanking":false}""").Settings);

        Assert.True(before.OwnBanking);
        Assert.Equal(75, before.FullAt);
        Assert.False(made.OwnBanking);
        Assert.False(chosen.OwnBanking);
        Assert.True(made.UseZaaps);
    }

    [Fact]
    public void AProfileThatHarvestedEverythingEachCharacterCanReadsAsEveryProfessionButBases()
    {
        var read = Assert.IsType<HarvestSettings>(Harvest().Settings.Read("""{"Resources":[],"EverythingKnown":true,"Area":1,"SubAreas":[]}""").Settings);

        Assert.Equal(HarvestMode.ByProfession, read.Mode);
        Assert.Equal(HarvestingModule.Professions, read.Jobs);
        Assert.Equal(HarvestOrder.Nearest, read.Order);
        Assert.Equal(HarvestPlace.Chosen, read.Place);
    }

    [Fact]
    public void TheHarvestGivesAnAutomaticPresetAndOnePerProfessionThatHoldAndOpenTheBags()
    {
        var settings = Harvest().Settings;
        var french = CultureInfo.GetCultureInfo("fr");

        Assert.Equal(["auto", "lumberjack", "miner", "alchemist", "farmer", "fisherman"], settings.Presets.Select(preset => preset.Key));
        Assert.Equal(
            ["Récolte automatique", "Bûcheron auto", "Mineur auto", "Alchimiste auto", "Paysan auto", "Pêcheur auto"],
            settings.Presets.Select(preset => preset.Name.In(french)));

        foreach (var preset in settings.Presets)
        {
            var read = Assert.IsType<HarvestSettings>(settings.Read(preset.Json).Settings);

            // Only the preset of every profession shares the zones between them (D391); a profession's own keeps its highest level first.
            var order = preset.Key is "auto" ? HarvestOrder.Balanced : HarvestOrder.HighestLevel;

            Assert.Equal((HarvestMode.ByProfession, order, HarvestPlace.Automatic, 10), (read.Mode, read.Order, read.Place, read.Rotation));
            Assert.True(read.OpenBags);
            Assert.Empty(settings.Check(read));
        }

        Assert.Equal(HarvestingModule.Professions, Assert.IsType<HarvestSettings>(settings.Read(settings.Presets[0].Json).Settings).Jobs);
        Assert.Equal([2], Assert.IsType<HarvestSettings>(settings.Read(settings.Presets[1].Json).Settings).Jobs);
    }

    [Fact]
    public void AProfileStoredBeforeTheRotationReadsAsTenSubAreasInTurn()
    {
        var read = Assert.IsType<HarvestSettings>(Harvest().Settings.Read("""{ "Mode": "ByProfession", "Jobs": [2], "Order": "HighestLevel", "Place": "Automatic" }""").Settings);

        Assert.Equal((10, HarvestOrder.HighestLevel), (read.Rotation, read.Order));
    }

    [Fact]
    public void TheRotationIsOneToTenSubAreasAndAskedOfAnAutomaticPlaceOnly()
    {
        var settings = Harvest().Settings;

        Assert.Empty(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2], Place = HarvestPlace.Automatic, Rotation = 10 }));
        Assert.NotEmpty(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2], Place = HarvestPlace.Automatic, Rotation = 11 }));
        Assert.NotEmpty(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2], Place = HarvestPlace.Automatic, Rotation = 0 }));
    }

    [Fact]
    public void ByProfessionAProfessionIsNeededAndAnAutomaticPlaceNeedsNoArea()
    {
        var settings = Harvest().Settings;

        Assert.Single(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Place = HarvestPlace.Automatic }));
        Assert.Empty(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2], Place = HarvestPlace.Automatic }));
        Assert.Single(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2] }));
    }
}
