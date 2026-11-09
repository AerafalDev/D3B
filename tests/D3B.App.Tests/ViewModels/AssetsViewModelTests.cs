using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Assets;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class AssetsViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private (AssetsViewModel Assets, ProfilesViewModel Profiles) Panel()
    {
        var profiles = new ProfilesViewModel(_localizer, new Dialogs(), new Toasts());
        var assets = new AssetsViewModel(_localizer, profiles.Dialogs, profiles.Toasts, profiles);

        profiles.Refresh(_store.Host.Services);
        assets.Refresh(_store.Host.Services);

        return (assets, profiles);
    }

    private static string Shape(AssetsViewModel assets)
    {
        return string.Join(" | ", assets.Rows.Select(row => new string(' ', row.Depth) + row.Kind switch
        {
            AssetRowKind.Folder => (row.IsExpanded ? "v " : "> ") + row.Name,
            AssetRowKind.Given => "# " + row.Name,
            AssetRowKind.Problem => "! " + row.Name,
            _ => row.Name + (row.Count.Length > 0 ? " " + row.Count : ""),
        }));
    }

    [Fact]
    public async Task TheTreeShowsTheProjectsFoldersAndAssetsThenTheGivenOnesWithHowManyUseEachAndAFileThatDoesNotRead()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Walks = 7 }), ct);

        await _store.Assets.CreateFolderAsync("Farm", ct);
        await _store.Assets.MoveAsync(walker.Id, "Farm", ct);
        await _store.Assets.CreateFolderAsync("Empty", ct);
        await File.WriteAllTextAsync(Path.Combine(_store.Assets.Folder, "Broken.profile.json"), "{", ct);
        await _store.Assets.LoadAsync([.. _store.Assets.Assets.Where(asset => asset.IsGiven)], ct);
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));
        await _store.Accounts.AssignAsync(TestPlay.Key, walker.Id, ct);

        var (assets, _) = Panel();
        var shape = Shape(assets);

        Assert.StartsWith("v Empty | v Farm |  Walker 1 | ! Broken.profile.json | # Given with D3B", shape);
        Assert.Equal("1", assets.Rows.Single(row => row.Name == "Farm").Count);
        Assert.Contains(assets.Rows, row => row is { IsProblem: true, Problem.Length: > 0 });
        Assert.All(assets.Rows.Where(row => row.IsGiven), row => Assert.Equal(1, row.Depth));
        Assert.Equal("Used by 1", assets.Rows.Single(row => row.Name == "Walker").CountTip);
        Assert.Equal(["Behavior", "Profile", "Route", "Zone"], assets.Kinds.Where(kind => kind.IsEnabled).Select(kind => kind.Name));
        Assert.Equal("7f", assets.Kinds.Single(kind => kind.Kind is AssetKind.List).Session);
    }

    [Fact]
    public async Task ABehaviorIsMadeAsAnEmptyListInTheFolderChosenAndOpensFromItsRow()
    {
        await _store.OpenedAsync();

        var (assets, _) = Panel();
        var sequences = new D3B.App.ViewModels.Sequences.SequencesViewModel(_localizer, new Dialogs(), new Toasts());
        var opened = new List<string>();

        sequences.Refresh(_store.Host.Services);
        sequences.Opened = editor => opened.Add(editor.Name);
        assets.Sequences = sequences;
        assets.New(AssetKind.Behavior);
        await Waiting.UntilAsync(() => opened.Count == 1, TimeSpan.FromSeconds(10));
        sequences.Refresh(null);
        assets.Refresh(_store.Host.Services);

        var row = assets.Rows.Single(item => item.Name == "New list of steps");

        Assert.Equal(AssetKind.Behavior, row.AssetKind);
        Assert.True(AssetsViewModel.CanOpen(row));

        var asked = new List<string>();

        assets.Open = asked.Add;
        assets.OpenRow(row);

        Assert.Equal([row.Id!], asked);
    }

    [Fact]
    public async Task AFolderFoldsAndTheSearchShowsOnlyWhatHoldsItsTextLitAndItsFoldersUnfolded()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", ct);

        await _store.Assets.SaveProfileAsync("Ash", TestStore.HarvestId, "{}", ct);
        await _store.Assets.MoveAsync(walker.Id, "Farm", ct);

        var (assets, _) = Panel();

        assets.Toggle(assets.Rows.Single(row => row.Name == "Farm"));

        Assert.DoesNotContain(assets.Rows, row => row.Name == "Walker");

        assets.Filter = "alk";

        var found = assets.Rows.Where(row => row.IsAsset).ToList();

        Assert.Equal(["Walker"], found.Select(row => row.Name));
        Assert.Equal(("W", "alk", "er"), (found[0].Before, found[0].Found, found[0].After));
        Assert.True(assets.Rows.Single(row => row.Name == "Farm").IsExpanded);

        assets.Filter = "nothing at all";

        Assert.True(assets.FindsNothing);
    }

    [Fact]
    public async Task AnAssetIsChosenRenamedInPlaceDuplicatedMovedAndAGivenOneCopiedIntoTheProject()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", ct);
        var chosen = new List<string>();
        var (assets, _) = Panel();

        assets.Choose = chosen.Add;
        assets.Click(assets.Rows.Single(row => row.Name == "Walker"));

        Assert.Equal([walker.Id], chosen);
        Assert.True(assets.Rows.Single(row => row.Name == "Walker").IsSelected);

        var row = assets.Rows.Single(item => item.Name == "Walker");

        assets.StartRename(row);
        row.EditName = "Strider";
        await assets.CommitRenameAsync(row);

        Assert.Equal("Strider", _store.Assets.Find(walker.Id)?.Name);

        await assets.DuplicateAsync(assets.Rows.Single(item => item.Name == "Strider"));

        Assert.Contains(_store.Assets.Assets, asset => asset.Name == "Strider (copy)");

        await assets.MoveAsync(assets.Rows.Single(item => item.Name == "Strider"), "Kept");

        Assert.Equal(Path.Combine("Kept", "Strider.profile.json"), _store.Assets.Find(walker.Id)?.Path);

        var given = assets.Rows.First(item => item is { IsGiven: true, IsAsset: true });

        await assets.CopyToProjectAsync(given);

        Assert.Contains(_store.Assets.Assets, asset => !asset.IsGiven && asset.Name == given.Name + " (copy)");
        Assert.Contains(assets.Rows, item => !item.IsGiven && item.Name == given.Name + " (copy)");
    }

    [Fact]
    public async Task ANewFolderIsNamedInPlaceAndAnImportGoesInTheFolderChosen()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var file = Path.Combine(Path.GetTempPath(), $"Imported{Guid.NewGuid():N}.profile.json");
        var (assets, _) = Panel();

        assets.NewFolderCommand.Execute(null);

        var row = Assert.Single(assets.Rows, item => item.IsNewFolder);

        Assert.True(row.IsRenaming);
        row.EditName = "Mines";
        await assets.CommitRenameAsync(row);

        Assert.Contains("Mines", _store.Assets.Folders);

        assets.Click(assets.Rows.Single(item => item.Name == "Mines"));
        await File.WriteAllTextAsync(file, $$"""{"id":"{{AssetLibrary.NewId()}}","kind":"profile","format":1,"behavior":"{{GivenAssets.BehaviorOf(TestStore.HarvestId)}}"}""", ct);

        try
        {
            assets.PickImport = () => Task.FromResult<IReadOnlyList<string>>([file]);
            await assets.ImportCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(file);
        }

        Assert.Contains(_store.Assets.Assets, asset => asset.Path == Path.Combine("Mines", Path.GetFileName(file)));
    }

    [Fact]
    public async Task AFolderIsRenamedInPlaceAndDeletedAfterAQuestionItsProfilesTabsClosed()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", ct);

        await _store.Assets.MoveAsync(walker.Id, "Farm", ct);

        var (assets, profiles) = Panel();
        var folder = assets.Rows.Single(row => row.Name == "Farm");

        assets.StartRename(folder);

        Assert.True(folder.IsRenaming);

        folder.EditName = "Fields";
        await assets.CommitRenameAsync(folder);

        Assert.Equal(Path.Combine("Fields", "Walker.profile.json"), _store.Assets.Find(walker.Id)?.Path);

        profiles.Find(walker.Id)!.Edit();

        Assert.Single(profiles.Open);

        assets.AskDelete(assets.Rows.Single(row => row.Name == "Fields"));

        var question = Assert.IsType<QuestionViewModel>(profiles.Dialogs.Current);

        Assert.Equal("Its 1 asset is deleted with it. It goes to the Windows recycle bin.", question.Message);

        question.ConfirmCommand.Execute(null);
        await Waiting.UntilAsync(() => _store.Assets.Find(walker.Id) is null, TimeSpan.FromSeconds(10));

        Assert.Empty(profiles.Open);
        Assert.DoesNotContain(assets.Rows, row => row.Name == "Fields");
    }

    [Fact]
    public async Task SeveralRowsAreChosenByCtrlAndShiftThenMovedTogetherAsADropWouldAndCutAndPasted()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;

        await _store.Assets.SaveProfileAsync("Ash", TestStore.HarvestId, "{}", ct);
        await _store.Assets.SaveProfileAsync("Oak", TestStore.HarvestId, "{}", ct);
        await _store.Assets.SaveProfileAsync("Yew", TestStore.HarvestId, "{}", ct);
        await _store.Assets.CreateFolderAsync("Trees", ct);
        await _store.Assets.CreateFolderAsync("Other", ct);

        var (assets, _) = Panel();

        assets.Click(assets.Rows.Single(row => row.Name == "Ash"));
        assets.Click(assets.Rows.Single(row => row.Name == "Yew"), range: true);

        Assert.Equal(["Ash", "Oak", "Yew"], assets.Rows.Where(row => row.IsSelected).Select(row => row.Name));

        assets.Click(assets.Rows.Single(row => row.Name == "Oak"), add: true);

        var keys = assets.DragKeys(assets.Rows.Single(row => row.Name == "Ash"));

        Assert.Equal(2, keys.Count);
        Assert.True(assets.CanMove(keys, "Trees"));
        Assert.False(assets.CanMove(keys, ""));

        await assets.MoveAsync(keys, "Trees");

        Assert.Equal(["Ash", "Yew"], _store.Assets.AssetsIn("Trees").Select(asset => asset.Name).Order());

        // A folder never goes into itself, and moves with what it holds.
        Assert.False(assets.CanMove(["folder:Trees"], "Trees"));
        await assets.MoveAsync(["folder:Trees"], "Other");

        Assert.Equal(Path.Combine("Other", "Trees", "Ash.profile.json"), _store.Assets.Assets.Single(asset => asset.Name == "Ash").Path);

        assets.Click(assets.Rows.Single(row => row.Name == "Oak"));
        assets.Cut();

        Assert.True(assets.HasCut);
        Assert.True(assets.Rows.Single(row => row.Name == "Oak").IsCut);

        assets.Click(assets.Rows.Single(row => row.Name == "Other"));
        await assets.PasteAsync();

        Assert.Equal(Path.Combine("Other", "Oak.profile.json"), _store.Assets.Assets.Single(asset => asset.Name == "Oak").Path);
        Assert.False(assets.HasCut);
    }

    [Fact]
    public async Task TheKeyboardMovesUnfoldsAndFoldsAndNothingChosenMakesAtTheRootUnderAFreeName()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", ct);

        await _store.Assets.MoveAsync(walker.Id, "Farm", ct);

        var folds = new List<IReadOnlyCollection<string>>();
        var (assets, _) = Panel();

        assets.FoldsChanged = folded => folds.Add([.. folded]);
        assets.MoveFocus(1);

        Assert.Equal("folder:Farm", assets.FocusKey);

        assets.Collapse();

        Assert.False(assets.Rows.Single(row => row.Name == "Farm").IsExpanded);
        Assert.Equal(["Farm"], folds[^1]);

        assets.Expand();
        assets.Expand();

        Assert.Equal(walker.Id, assets.FocusKey);

        assets.Collapse();

        Assert.Equal("folder:Farm", assets.FocusKey);

        assets.ClearSelection();
        assets.NewFolderCommand.Execute(null);

        var first = Assert.Single(assets.Rows, row => row.IsNewFolder);

        Assert.Equal((0, "New folder"), (first.Depth, first.EditName));
        await assets.CommitRenameAsync(first);

        assets.ClearSelection();
        assets.NewFolderCommand.Execute(null);

        Assert.Equal("New folder 2", Assert.Single(assets.Rows, row => row.IsNewFolder).EditName);
        Assert.Contains("New folder", _store.Assets.Folders);
    }

    [Fact]
    public async Task AKindHiddenLeavesTheTreeAndTheOpenTabsAssetIsLocatedItsFoldersUnfolded()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, "{}", ct);

        await _store.Assets.MoveAsync(walker.Id, Path.Combine("Farm", "Deep"), ct);

        var (assets, _) = Panel();

        assets.ToggleKind(D3B.Studio.Assets.AssetKind.Profile);

        Assert.True(assets.IsFiltered);
        Assert.DoesNotContain(assets.Rows, row => row.AssetKind is D3B.Studio.Assets.AssetKind.Profile);

        assets.ShowAllKinds();
        assets.CollapseAll();

        Assert.DoesNotContain(assets.Rows, row => row.Name == "Walker");

        assets.OpenAssetId = () => walker.Id;
        assets.LocateOpenCommand.Execute(null);

        Assert.True(assets.Rows.Single(row => row.Name == "Walker").IsSelected);
        Assert.Equal(walker.Id, assets.ChosenId);
    }
}
