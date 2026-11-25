using D3B.App.Localization;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Settings;
using D3B.Bot.Transfers;
using D3B.Data.Items;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class TransferFormTests
{
    private static (SettingsEditor Editor, TransferSettings Draft) Edit(TransferSettings draft, bool french = false)
    {
        var services = new ServiceCollection();

        TransfersModule.Register(services);

        var definition = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        var localizer = new Localizer(french ? Localizer.French : Localizer.English);

        return (new SettingsEditor(definition.Settings, draft, TestReferences.Game(), new GameNames(null, localizer), localizer), draft);
    }

    private static SettingFieldViewModel Field(SettingsEditor editor, string key)
    {
        return editor.Sections.SelectMany(section => section.Fields).Single(field => field.Definition.Key == key);
    }

    [Fact]
    public void TheFormAsksWhatThenHowManyThenTheOptionsInFrench()
    {
        var (editor, _) = Edit(new TransferSettings(), french: true);

        Assert.Equal(["Quoi", "Combien", "Options"], editor.Sections.Select(section => section.Title));
        Assert.Equal(["Sens", "Tout ce que la source contient", "Catégories", "Types", "Objets"], editor.Sections[0].InView.Select(field => field.Label));
        Assert.Equal(["Types exclus", "Objets exclus"], editor.Sections[0].Advanced.Select(field => field.Label));
    }

    [Fact]
    public void TheTypesAreChosenAmongThoseOfThePackThatHoldAnItem()
    {
        var (editor, draft) = Edit(new TransferSettings());
        var types = Assert.IsType<GamesFieldViewModel>(Field(editor, nameof(TransferSettings.Types)));

        Assert.Equal([38, 39], types.Available.Select(option => (int)option.Value).Order());
        Assert.Equal("Item type 38", types.Available.Single(option => (int)option.Value == 38).Name);

        types.ToAdd = types.Available.Single(option => (int)option.Value == 39);

        Assert.Equal([39], draft.Types);
        Assert.False(editor.HasProblems);
    }

    [Fact]
    public void NothingChosenIsAProblemAndTheNumberShowsOnlyForAQuantityThatNeedsIt()
    {
        var (editor, draft) = Edit(new TransferSettings());
        var amount = Field(editor, nameof(TransferSettings.Amount));

        Assert.True(editor.HasProblems);
        Assert.False(amount.IsVisible);

        var categories = Assert.IsType<ChoicesFieldViewModel>(Field(editor, nameof(TransferSettings.Categories)));

        categories.ToAdd = categories.Available.Single(option => Equals(option.Value, ItemCategory.Resource));
        draft.Quantity = TransferQuantity.KeepInSource;
        editor.Changed();

        Assert.Equal([ItemCategory.Resource], draft.Categories);
        Assert.False(editor.HasProblems);
        Assert.True(amount.IsVisible);
    }
}
