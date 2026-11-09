using D3B.App.Localization;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

/// <summary>A profile's settings in categories, the rare ones folded under "Advanced" and counted when they differ from the model (D415).</summary>
[UiThread]
public sealed class SettingsCategoriesTests
{
    private static SettingsEditor Edit(HarvestSettings draft, HarvestSettings? defaults = null, Localizer? localizer = null)
    {
        var harvest = new ServiceCollection();

        HarvestingModule.Register(harvest);

        var definition = Assert.Single(harvest.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        localizer ??= new Localizer(Localizer.English);

        var game = new GameReferences(TestReferences.Atlas(), TestReferences.Jobs(), TestReferences.Items());

        return new SettingsEditor(definition.Settings, draft, game, new GameNames(null, localizer), localizer, defaults: defaults);
    }

    private static string[] Keys(IEnumerable<SettingFieldViewModel> fields)
    {
        return [.. fields.Where(field => field.IsVisible).Select(field => field.Definition.Key)];
    }

    [Fact]
    public void AProfileHasFourCategoriesEachKeepingItsSettingsInViewApartFromItsAdvancedOnes()
    {
        var editor = Edit(new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash] });
        var (what, where, bank, care) = (editor.Sections[0], editor.Sections[1], editor.Sections[2], editor.Sections[3]);

        Assert.Equal(["What", "Where", "Bank", "Care and fights"], editor.Sections.Select(section => section.Title));
        Assert.Equal(["Mode", "Resources"], Keys(what.InView));
        Assert.False(what.HasAdvanced);
        Assert.Equal(["Place", "Area", "SubAreas"], Keys(where.InView));
        Assert.Equal(["IsolatedMaps", "ZaapSaving"], Keys(where.Advanced));
        Assert.Equal(["Deposit"], Keys(bank.InView));
        Assert.Equal(["OwnBanking", "DepositTo"], Keys(bank.Advanced));
        Assert.Contains("AiFights", Keys(care.InView));
        Assert.Contains("AfterFight", Keys(care.Advanced));
        Assert.All(editor.Sections, section => Assert.Equal(section.Fields.Count, section.InView.Count + section.Advanced.Count));
    }

    [Fact]
    public void AnAdvancedSettingThatDiffersFromTheModelIsCountedOnTheFoldAndItsCategoryIsMarked()
    {
        var model = new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash] };
        var draft = new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash] };
        var editor = Edit(draft, model);
        var where = editor.Sections[1];

        Assert.Equal((0, "Advanced", false), (where.AdvancedChanged, where.AdvancedText, where.HasChanges));
        Assert.False(where.IsAdvancedOpen);

        ((NumberFieldViewModel)editor.FieldOf("ZaapSaving")!).Value = 12;

        Assert.Equal((1, "Advanced · 1 changed", true), (where.AdvancedChanged, where.AdvancedText, where.HasChanges));
        Assert.Equal(1, editor.ChangedCount);
        Assert.False(where.IsAdvancedOpen);

        ((FlagFieldViewModel)editor.FieldOf("IsolatedMaps")!).Value = true;

        Assert.Equal("Advanced · 2 changed", where.AdvancedText);

        ((NumberFieldViewModel)editor.FieldOf("ZaapSaving")!).Value = model.ZaapSaving;
        ((FlagFieldViewModel)editor.FieldOf("IsolatedMaps")!).Value = false;

        Assert.Equal((0, "Advanced", false), (where.AdvancedChanged, where.AdvancedText, where.HasChanges));
    }

    [Fact]
    public void TheFoldIsNamedInFrenchAndCountsOnlyTheAdvancedSettingsThatShow()
    {
        var model = new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash], OwnBanking = true, FullAt = 80 };
        var draft = new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash], OwnBanking = true, FullAt = 60 };
        var editor = Edit(draft, model, new Localizer(Localizer.French));
        var bank = editor.Sections[2];

        Assert.Equal("Banque", bank.Title);
        Assert.Equal("Avancé · 1 changé", bank.AdvancedText);

        ((FlagFieldViewModel)editor.FieldOf("OwnBanking")!).Value = false;

        // The share of pods hides with the rule it belongs to and is no longer counted; the rule itself now differs from the model.
        Assert.Equal(["OwnBanking", "DepositTo"], Keys(bank.Advanced));
        Assert.Equal("Avancé · 1 changé", bank.AdvancedText);
    }

    [Fact]
    public void TheFoldOpensByTheHandAndOpensByItselfWhenAnAdvancedSettingHasAProblem()
    {
        var editor = Edit(new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash] });
        var where = editor.Sections[1];

        Assert.False(where.ShowsAdvanced);

        where.ToggleAdvancedCommand.Execute(null);

        Assert.True(where.ShowsAdvanced);

        where.ToggleAdvancedCommand.Execute(null);

        Assert.False(where.ShowsAdvanced);

        // An empty number is no value: the fold shows it, whatever the hand left.
        ((NumberFieldViewModel)editor.FieldOf("ZaapSaving")!).Value = null;

        Assert.False(where.IsAdvancedOpen);
        Assert.True(where.ShowsAdvanced);
        Assert.True(editor.HasProblems);
    }

    [Fact]
    public void ACategoryWhoseAdvancedSettingsAllHideHasNoFold()
    {
        var editor = Edit(new HarvestSettings { Mode = HarvestMode.List, Resources = [TestReferences.Ash] });

        Assert.False(editor.Sections[0].HasAdvanced);
        Assert.True(editor.Sections[1].HasAdvanced);

        var mode = (ChoiceFieldViewModel)editor.FieldOf("Mode")!;

        mode.Selected = mode.Options.Single(option => option.Value.Equals(HarvestMode.ByProfession));

        Assert.True(editor.Sections[0].HasAdvanced);
    }
}
