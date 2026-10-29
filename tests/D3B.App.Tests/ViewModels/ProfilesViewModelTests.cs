using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;

namespace D3B.App.Tests.ViewModels;

public sealed class ProfilesViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    private static T Field<T>(ProfileViewModel item, string label)
        where T : SettingFieldViewModel
    {
        return item.Editor!.Sections.SelectMany(section => section.Fields).OfType<T>().Single(field => field.Label == label);
    }

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task EachProfileShowsItsActivityAFewSettingsAndItsCharacters()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var lumberjack = await _store.Profiles.SaveAsync(0, "Lumberjack", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack, Resources = [TestReferences.Chestnut, TestReferences.Ash, TestReferences.Iron] }), ct);

        await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", ct);
        await _store.Profiles.SaveAsync(0, "Gone", "gone", "{}", ct);
        await _store.Profiles.SaveAsync(0, "Old", TestStore.HarvestId, """{"Focus":"Dungeon"}""", ct);
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200), (70_002, "Kolossa", 87));
        await _store.Accounts.AssignAsync(TestPlay.Key, lumberjack.Id, ct);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        Assert.True(page.IsAvailable);
        Assert.Equal("4 profiles, 1 character has one", page.Summary);
        Assert.Equal(["Gone", "Lumberjack", "Old", "Stroll"], page.Items.Select(item => item.Name));

        var item = page.Items[1];

        Assert.Equal("Harvest", item.ActivityName);
        Assert.Equal("Focus: On Job · Job: Lumberjack · Resources: Chestnut, Ash and 1 more", item.Summary);
        Assert.Equal(["AE"], item.Players);
        Assert.Equal("1 character", item.PlayersText);
        Assert.False(item.NeedsReview);
        Assert.Equal("Activity gone: gone", page.Items[0].Review);
        Assert.Equal("To review", page.Items[2].Review);
        Assert.Equal("No character", page.Items[3].PlayersText);
        Assert.Equal("", page.Items[3].Summary);

        _localizer.Use(Localizer.French);
        page.Refresh(_store.Host.Services);

        Assert.Equal("Focus : On Job · Job : Bûcheron · Resources : Châtaignier, Frêne et 1 de plus", page.Items[1].Summary);
    }

    [Fact]
    public async Task ANewProfileIsSavedOnceItsNameAndSettingsHold()
    {
        await _store.OpenedAsync();

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);
        Assert.True(page.IsEmpty);

        page.NewCommand.Execute(null);

        var item = Assert.Single(page.Items);

        Assert.True(item.IsNew && item.IsEditing);
        Assert.Equal("Harvest", item.EditActivity?.Name);
        Assert.Equal("Give the profile a name", item.NameProblem);
        Assert.Equal("New profile", item.Title);
        Assert.False(item.SaveCommand.CanExecute(null));

        var heard = new List<string?>();

        item.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        item.EditName = "Lumberjack";

        // Its header says the name as it is typed.
        Assert.Equal("Lumberjack", item.Title);

        // The profession has no default: the user must choose one.
        Assert.Equal("Required", Field<GameFieldViewModel>(item, "Job").Problem);
        Assert.False(item.CanSave);

        var job = Field<GameFieldViewModel>(item, "Job");

        job.Selected = job.Options.Single(option => option.Name == "Lumberjack");

        Assert.True(item.CanSave);

        await item.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Profiles.Profiles);

        Assert.False(item.IsEditing);
        Assert.Equal(("Lumberjack", TestStore.HarvestId), (saved.Name, saved.ActivityId));
        Assert.Contains($"\"Job\":{TestReferences.Lumberjack}", saved.Settings, StringComparison.Ordinal);

        // Saved, it is new no more, and the view hears it: its header keeps its name.
        Assert.False(item.IsNew);
        Assert.Contains(nameof(ProfileViewModel.IsNew), heard);
        Assert.Equal("Lumberjack", item.Title);

        page.Refresh(_store.Host.Services);

        Assert.Same(item, Assert.Single(page.Items));
        Assert.Equal(saved.Id, item.Id);
    }

    [Fact]
    public async Task ANameAnotherProfileHasCannotBeSaved()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);
        page.NewCommand.Execute(null);

        var item = page.Items[0];

        item.EditActivity = item.Activities.Single(activity => activity.Name == "Stroll");
        item.EditName = "stroll";

        Assert.Equal("Another profile has this name", item.NameProblem);
        Assert.False(item.CanSave);

        item.EditName = "Second stroll";

        Assert.True(item.CanSave);
        Assert.True(item.Editor!.IsEmpty);
    }

    [Fact]
    public async Task EachSettingIsEditedByItsKindAndCheckedAsItChanges()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Lumberjack", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack }), TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = Assert.Single(page.Items);

        item.EditCommand.Execute(null);

        var editor = item.Editor!;

        Assert.Equal(["What", "Where", "How"], editor.Sections.Select(section => section.Title));
        Assert.True(item.CanSave);

        // Resources are offered within the profession chosen, by the level it gathers them from, each saying whose it is.
        var resources = Field<GamesFieldViewModel>(item, "Resources");

        Assert.Equal(["Ash", "Chestnut"], resources.Available.Select(option => option.Name));
        Assert.Equal(["Lumberjack, level 1", "Lumberjack, level 10"], resources.Available.Select(option => option.Detail));

        resources.ToAdd = resources.Available[1];
        resources.ToAdd = resources.Available[0];

        Assert.Equal(["Chestnut", "Ash"], resources.Items.Select(row => row.Option.Name));
        Assert.Empty(resources.Available);
        Assert.Equal("", resources.Search);

        resources.Items[1].MoveUpCommand.Execute(null);

        Assert.Equal(["Ash", "Chestnut"], resources.Items.Select(row => row.Option.Name));
        Assert.False(resources.Items[0].CanMoveUp);

        // Another profession: what was chosen of the first is not within it.
        var job = Field<GameFieldViewModel>(item, "Job");

        job.Selected = job.Options.Single(option => option.Name == "Miner");

        Assert.Equal(["Iron"], resources.Available.Select(option => option.Name));
        Assert.Equal("Not within “Job”", resources.Problem);
        Assert.False(item.CanSave);

        resources.Items[0].RemoveCommand.Execute(null);
        resources.Items[0].RemoveCommand.Execute(null);

        Assert.True(item.CanSave);

        // A hidden setting is not checked; a rule that does not hold is said apart.
        var focus = Field<ChoiceFieldViewModel>(item, "Focus");

        job.ClearCommand.Execute(null);
        Assert.False(item.CanSave);
        focus.Selected = focus.Options.Single(option => option.Name == "On Area");
        Assert.False(job.IsVisible);
        Assert.True(item.CanSave);

        Field<NumberFieldViewModel>(item, "Lowest").Value = 6;
        Field<NumberFieldViewModel>(item, "Highest").Value = 4;

        Assert.Equal("The lowest is above the highest.", editor.Rules);
        Assert.False(item.CanSave);

        Field<NumberFieldViewModel>(item, "Highest").Value = 8;
        Field<NumberFieldViewModel>(item, "Walks").Value = null;

        Assert.Equal("Required", Field<NumberFieldViewModel>(item, "Walks").Problem);
        Assert.False(item.CanSave);

        Field<NumberFieldViewModel>(item, "Walks").Value = 50;
        Field<TextFieldViewModel>(item, "Greeting").Value = "Bonjour à tous";

        Assert.Equal("10 characters at most", Field<TextFieldViewModel>(item, "Greeting").Problem);

        Field<TextFieldViewModel>(item, "Greeting").Value = "Salut";
        Field<FlagFieldViewModel>(item, "Bank").Value = false;

        var order = Field<ChoicesFieldViewModel>(item, "Order");

        order.ToAdd = order.Available.Single(option => option.Name == "On Nothing");

        Assert.Equal(["On Area", "On Nothing"], order.Items.Select(row => row.Option.Name));
        Assert.True(item.CanSave);

        await item.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Profiles.Profiles);
        var settings = Assert.IsType<TestSettings>(TestStore.Harvest().Settings.Read(saved.Settings).Settings);

        Assert.Equal((TestFocus.Area, (int?)null, 50, false, "Salut", 6, 8), (settings.Focus, settings.Job, settings.Walks, settings.UseBank, settings.Greeting, settings.Lowest, settings.Highest));
        Assert.Equal([TestFocus.Area, TestFocus.Nothing], settings.Order);
        Assert.Empty(settings.Resources);
    }

    [Fact]
    public async Task SubAreasAreSoughtWithinTheAreaChosenAndFoundWhateverTheAccents()
    {
        await _store.OpenedAsync();
        _localizer.Use(Localizer.French);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);
        page.NewCommand.Execute(null);

        var item = page.Items[0];
        var area = Field<GameFieldViewModel>(item, "Area");
        var subAreas = Field<GamesFieldViewModel>(item, "Sub-areas");

        Assert.Equal(["Cité d'Astrub", "Collines d'Amakna", "Forêt d'Amakna"], subAreas.Available.Select(option => option.Name));

        area.Selected = area.Options.Single(option => option.Name == "Amakna");

        Assert.Equal(["Collines d'Amakna", "Forêt d'Amakna"], subAreas.Available.Select(option => option.Name));
        Assert.True(OptionViewModel.Filter("foret", subAreas.Available[1]));
        Assert.True(OptionViewModel.Filter("COLLINES", subAreas.Available[0]));
        Assert.False(OptionViewModel.Filter("astrub", subAreas.Available[0]));
    }

    [Fact]
    public async Task ChangingTheActivityStartsItsSettingsFromTheirDefaults()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Lumberjack", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack, Walks = 42 }), TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = page.Items[0];

        item.EditCommand.Execute(null);

        Assert.Equal(42, Field<NumberFieldViewModel>(item, "Walks").Value);

        item.EditActivity = item.Activities.Single(activity => activity.Name == "Stroll");

        Assert.True(item.Editor!.IsEmpty);

        item.EditActivity = item.Activities.Single(activity => activity.Name == "Harvest");

        Assert.Equal(10, Field<NumberFieldViewModel>(item, "Walks").Value);

        item.CancelCommand.Execute(null);

        Assert.False(item.IsEditing);
        Assert.Null(item.Editor);
    }

    [Fact]
    public async Task AProfileBeingEditedKeepsItsEditorWhileTheRelayRecords()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = page.Items[0];

        item.EditCommand.Execute(null);
        item.EditName = "Long stroll";

        var editor = item.Editor;

        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));
        page.Refresh(_store.Host.Services);

        Assert.Same(item, page.Items[0]);
        Assert.Same(editor, item.Editor);
        Assert.Equal("Long stroll", item.EditName);
    }

    [Fact]
    public async Task OnlyOneProfileIsEditedAtOnceAndANewOneUnsavedGoesWithItsEditor()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);
        page.NewCommand.Execute(null);

        Assert.Equal(2, page.Items.Count);

        page.Items[1].EditCommand.Execute(null);

        Assert.Single(page.Items);
        Assert.True(page.Items[0].IsEditing);
    }

    [Fact]
    public async Task DeletingAProfileAsksFirst()
    {
        await _store.OpenedAsync();
        await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", TestContext.Current.CancellationToken);

        var page = new ProfilesViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = page.Items[0];

        item.EditCommand.Execute(null);
        item.AskDeleteCommand.Execute(null);

        Assert.True(item.IsConfirmingDelete);

        item.KeepItCommand.Execute(null);

        Assert.False(item.IsConfirmingDelete);
        Assert.Single(_store.Profiles.Profiles);

        item.AskDeleteCommand.Execute(null);
        await item.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_store.Profiles.Profiles);
        Assert.Empty(page.Items);
        Assert.True(page.IsEmpty);
    }
}
