using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Assets;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Tests.Behaviors;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;

namespace D3B.App.Tests.ViewModels;

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
        Assert.Equal(["Profile"], assets.Kinds.Where(kind => kind.IsEnabled).Select(kind => kind.Name));
        Assert.Equal("7e", assets.Kinds.Single(kind => kind.Kind is AssetKind.Behavior).Session);
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

        assets.Select(assets.Rows.Single(row => row.Name == "Farm"));

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
        assets.Select(assets.Rows.Single(row => row.Name == "Walker"));

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

        assets.ChooseFolder(assets.Rows.Single(item => item.Name == "Mines"));
        await File.WriteAllTextAsync(file, $$"""{"id":"{{AssetLibrary.NewId()}}","kind":"profile","format":1,"behavior":"{{GivenAssets.BehaviorOf(TestStore.HarvestId)}}"}""", ct);

        try
        {
            assets.PickImport = () => Task.FromResult<string?>(file);
            await assets.ImportCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(file);
        }

        Assert.Contains(_store.Assets.Assets, asset => asset.Path == Path.Combine("Mines", Path.GetFileName(file)));
    }
}
