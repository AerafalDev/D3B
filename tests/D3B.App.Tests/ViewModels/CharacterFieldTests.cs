using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Groups;
using D3B.Bot.Network;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

/// <summary>The field that picks a character of the accounts page, a follower's master (D436).</summary>
[UiThread]
public sealed class CharacterFieldTests
{
    private readonly Dialogs _dialogs = new();

    private (SettingsEditor Editor, CharacterFieldViewModel Field) Edit(TestStore store, FollowSettings draft)
    {
        var services = new ServiceCollection();

        GroupsModule.Register(services);

        var definition = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        var localizer = new Localizer(Localizer.English);
        var editor = new SettingsEditor(definition.Settings, draft, null, new GameNames(null, localizer), localizer, store.Host.Services, dialogs: _dialogs);

        return (editor, Assert.IsType<CharacterFieldViewModel>(editor.FieldOf(nameof(FollowSettings.Master))));
    }

    [Fact]
    public async Task PickingACharacterNamesItAndWritesItsKey()
    {
        await using var store = await new TestStore().OpenedAsync();

        await store.SeeAsync(new GameAccount(7, "first", "1"), (70_001, "Zelda", 120), (70_002, "Anna", null));
        await store.SeeAsync(new GameAccount(8, "second", "2"), (80_001, "Bob", 45));

        var draft = new FollowSettings();
        var (editor, field) = Edit(store, draft);

        Assert.Equal("choose a character", field.Text);
        Assert.True(field.CanPick);
        Assert.True(editor.HasProblems);

        field.PickCommand.Execute(null);

        var picker = Assert.IsType<RoutePickerViewModel>(_dialogs.Current);

        Assert.Equal(["Anna", "Bob", "Zelda"], picker.Routes.Select(character => character.Name));
        Assert.Equal(["first", "Level 45 · second", "Level 120 · first"], picker.Routes.Select(character => character.Detail));
        picker.Routes[1].ChooseCommand.Execute(null);

        Assert.Equal("8:80001", draft.Master);
        Assert.Equal("Bob", field.Text);
        Assert.False(editor.HasProblems);

        field.ClearCommand.Execute(null);

        Assert.Equal("", draft.Master);
        Assert.True(editor.HasProblems);
    }

    [Fact]
    public async Task ACharacterD3BNoLongerKnowsBlocksTheProfileAndSaysWhy()
    {
        await using var store = await new TestStore().OpenedAsync();

        await store.SeeAsync(new GameAccount(7, "first", "1"), (70_001, "Zelda", 120));

        var (editor, field) = Edit(store, new FollowSettings { Master = "7:99999" });

        Assert.Equal("7:99999 (unknown)", field.Text);
        Assert.Equal("D3B no longer knows this character.", field.Problem);
        Assert.True(editor.HasProblems);
    }
}
