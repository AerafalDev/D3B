using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Sequences;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class StepsProfilesTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static NodeCall Step(string node, params (string Key, JsonNode? Value)[] values)
    {
        return new NodeCall(node, values.ToImmutableSortedDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
    }

    private async Task<(ProfilesViewModel Page, SequencesViewModel Sequences)> PagesAsync()
    {
        await _store.OpenedAsync();

        var dialogs = new Dialogs();
        var toasts = new Toasts();
        var sequences = new SequencesViewModel(_localizer, dialogs, toasts);
        var page = new ProfilesViewModel(_localizer, dialogs, toasts) { Sequences = sequences };

        sequences.Refresh(_store.Host.Services);
        page.Refresh(_store.Host.Services);

        return (page, sequences);
    }

    private async Task<BehaviorAsset> ListAsync(string name, params NodeCall[] steps)
    {
        return await _store.Assets.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = name }, new BehaviorSequence([.. steps], StepRepeat.Once)), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AProfileThatPlaysANewListMakesTheListWithItAndOpensBothInTheirTabs()
    {
        var (page, sequences) = await PagesAsync();
        var opened = new List<string>();

        page.Opened = item => opened.Add("profile " + item.EditName);
        sequences.Opened = editor => opened.Add("list " + editor.Name);
        page.NewCommand.Execute(null);

        var dialog = Assert.IsType<NewProfileViewModel>(page.Dialogs.Current);

        Assert.True(dialog.CanChoosePlay);
        Assert.False(dialog.PlaysBehavior);
        Assert.Equal("A new list of steps", dialog.Behaviors[0].Name);

        dialog.Name = "Wood run";
        dialog.PlaysBehavior = true;

        Assert.True(dialog.CanCreate);

        dialog.CreateCommand.Execute(null);
        await Waiting.UntilAsync(() => opened.Count == 2, TimeSpan.FromSeconds(10));

        var profile = Assert.IsType<ProfileViewModel>(page.Editing);
        var list = Assert.Single(sequences.Open);

        Assert.Equal(["profile Wood run", "list Wood run"], opened);
        Assert.True(profile.IsBehaviorProfile && profile.IsNew && profile.IsEditing);
        Assert.False(profile.IsActivityMissing);
        Assert.Equal("Wood run", profile.BehaviorName);
        Assert.Equal("No step yet", profile.BehaviorDetail);
        Assert.True(profile.CanEditSteps);
        Assert.Null(profile.Editor);
        Assert.True(profile.CanSave);

        await profile.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Assets.OfType<ProfileAsset>(), asset => !asset.IsGiven);

        Assert.Equal((list.Id, "Wood run"), (saved.Behavior, saved.Name));
        Assert.Empty(saved.Values);
        Assert.False(profile.IsDirty);
        Assert.Equal("Wood run", page.Items.Single(item => item.Id == saved.Id).ActivityName);
    }

    [Fact]
    public async Task AProfileMayPlayAListOfTheProjectAndTheDialogSaysWhatEachIsMadeOf()
    {
        var (page, sequences) = await PagesAsync();
        var list = await ListAsync("Two steps", Step("note", ("Text", "a")), Step("stop"));

        await _store.Assets.SaveAsync(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = "Canvas", Always = [new BehaviorState("safe", "Safe")] }, TestContext.Current.CancellationToken);
        page.NewCommand.Execute(null);

        var dialog = Assert.IsType<NewProfileViewModel>(page.Dialogs.Current);

        Assert.Equal(["A new list of steps", "Canvas", "Two steps"], dialog.Behaviors.Select(choice => choice.Name));
        Assert.Equal(["Made with the profile, named after it", "More than a list of steps", "2 steps"], dialog.Behaviors.Select(choice => choice.Detail));

        dialog.Name = "Run";
        dialog.PlaysBehavior = true;
        dialog.Behavior = dialog.Behaviors.Single(choice => choice.Id == list.Id);
        dialog.CreateCommand.Execute(null);

        var profile = Assert.IsType<ProfileViewModel>(page.Editing);

        Assert.True(profile.IsBehaviorProfile);
        Assert.Equal("Two steps", profile.BehaviorName);
        Assert.Equal("2 steps", profile.BehaviorDetail);
        Assert.Empty(sequences.Open);

        await profile.SaveCommand.ExecuteAsync(null);

        Assert.Equal(list.Id, Assert.Single(_store.Assets.OfType<ProfileAsset>(), asset => !asset.IsGiven).Behavior);

        profile.EditStepsCommand.Execute(null);

        Assert.Equal(list.Id, Assert.Single(sequences.Open).Id);
    }

    [Fact]
    public async Task ASavedProfileOfAListShowsItsListAndNeedsNoReviewAndItsNameIsEditable()
    {
        var (page, _) = await PagesAsync();
        var list = await ListAsync("Loop", Step("note", ("Text", "a")));
        var profile = await _store.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Mine", Behavior = list.Id }, TestContext.Current.CancellationToken);

        page.Refresh(null);

        var item = page.Find(profile.Id)!;

        Assert.True(item.IsBehaviorProfile);
        Assert.False(item.NeedsReview);
        Assert.Equal("Loop", item.ActivityName);
        Assert.Equal("1 step", item.BehaviorDetail);

        item.EditCommand.Execute(null);

        Assert.True(item.IsEditing);
        Assert.False(item.IsDirty);
        Assert.True(item.CanSave);

        item.EditName = "Renamed";

        Assert.True(item.IsDirty);
        Assert.True(item.CanSave);

        await item.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Renamed"], _store.Assets.OfType<ProfileAsset>().Where(asset => !asset.IsGiven).Select(asset => asset.Name));
        Assert.Equal(list.Id, _store.Assets.Find<ProfileAsset>(profile.Id)!.Behavior);
    }

    [Fact]
    public async Task AProfileKeepsWhatItChangedOfItsBehaviorWhenItIsSavedAgain()
    {
        var (page, _) = await PagesAsync();
        var list = await ListAsync("Loop", Step("stop"));
        var profile = await _store.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Mine", Behavior = list.Id, Values = ImmutableSortedDictionary.Create<string, JsonNode?>(StringComparer.Ordinal).Add("Later", 3) }, TestContext.Current.CancellationToken);

        page.Refresh(null);

        var item = page.Find(profile.Id)!;

        item.EditCommand.Execute(null);
        item.EditName = "Again";
        await item.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Later"], _store.Assets.Find<ProfileAsset>(profile.Id)!.Values.Keys);
    }

    [Fact]
    public async Task ADuplicateOfAProfileOfAListPlaysTheSameListUnderANameOfItsOwn()
    {
        var (page, _) = await PagesAsync();
        var list = await ListAsync("Loop", Step("stop"));
        var profile = await _store.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Mine", Behavior = list.Id }, TestContext.Current.CancellationToken);

        page.Refresh(null);
        page.Find(profile.Id)!.DuplicateCommand.Execute(null);

        var dialog = Assert.IsType<NewProfileViewModel>(page.Dialogs.Current);

        Assert.False(dialog.CanChoosePlay);
        Assert.True(dialog.PlaysBehavior);
        Assert.Equal("Mine (copy)", dialog.Name);
        Assert.Equal(list.Id, dialog.Behavior?.Id);

        dialog.CreateCommand.Execute(null);

        var copy = Assert.IsType<ProfileViewModel>(page.Editing);

        await copy.SaveCommand.ExecuteAsync(null);

        Assert.Equal([list.Id, list.Id], _store.Assets.OfType<ProfileAsset>().Where(asset => !asset.IsGiven).Select(asset => asset.Behavior));
    }

    [Fact]
    public async Task AProfileOfAnActivityIsNotAffectedByTheLists()
    {
        var (page, _) = await PagesAsync();

        await ListAsync("Loop", Step("stop"));
        await _store.Assets.SaveProfileAsync("Stroll", TestStore.StrollId, "{}", TestContext.Current.CancellationToken);
        page.Refresh(null);

        var item = Assert.Single(page.Items, profile => profile.Name == "Stroll");

        Assert.False(item.IsBehaviorProfile);
        Assert.Equal("Stroll", item.ActivityName);
        Assert.False(item.CanEditSteps);
    }

    [Fact]
    public async Task AProfileOfAListIsStartedOnACharacterLikeAnyOther()
    {
        var (_, _) = await PagesAsync();
        var list = await ListAsync("Notes", Step("note", ("Text", "hello")));
        var profile = await _store.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Mine", Behavior = list.Id }, TestContext.Current.CancellationToken);
        var (_, bot) = await _store.Play.EnterAsync(300);
        var starter = (ProfileStarter)_store.Host.Services.GetService(typeof(ProfileStarter))!;
        var start = starter.Start(bot, profile);

        Assert.Null(start.Failure);
        Assert.Equal(D3B.Bot.Activities.ActivityOutcome.Done, await start.Run!.Completion);
        Assert.Equal(["hello"], bot.Journal.All().OfType<D3B.Bot.Journal.NoteEntry>().Select(note => note.Text));
    }
}
