using D3B.Bot.Assets;
using D3B.Data.Tests;

namespace D3B.Bot.Tests.Assets;

public sealed class AssetLibraryTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly AssetLibrary _library;

    public AssetLibraryTests()
    {
        _library = new AssetLibrary(new StudioSettings { Path = _directory.Combine("studio") });
    }

    public void Dispose()
    {
        _library.Dispose();
        _directory.Dispose();
    }

    private static BehaviorAsset Given()
    {
        return new BehaviorAsset { Id = "d3b.harvest", Name = "Harvest", Title = D3B.Bot.Localization.LocalizedText.Invariant("Harvest") };
    }

    private void WriteFile(string path, string json)
    {
        var file = _directory.Combine("studio", path);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
    }

    [Fact]
    public async Task TheFolderIsReadWithItsOwnFoldersAndAFileThatDoesNotReadIsSaid()
    {
        WriteFile("Mine.profile.json", """{"id":"p1","kind":"profile","format":1,"behavior":"d3b.harvest"}""");
        WriteFile(Path.Combine("farm", "Wheat.profile.json"), """{"id":"p2","kind":"profile","format":1,"behavior":"d3b.harvest"}""");
        WriteFile("Other.profile.json", """{"id":"p1","kind":"profile","format":1,"behavior":"d3b.harvest"}""");
        WriteFile("Broken.profile.json", "{");
        WriteFile("notes.json", "{}");

        await _library.LoadAsync([Given()], TestContext.Current.CancellationToken);

        Assert.Equal(["Harvest", "Mine", "Wheat"], _library.Assets.Select(asset => asset.Name));
        Assert.Equal(Path.Combine("farm", "Wheat.profile.json"), _library.Find("p2")?.Path);
        Assert.Equal([("Broken.profile.json", AssetProblemKind.NotJson), ("Other.profile.json", AssetProblemKind.IdTaken)], _library.Problems.Select(problem => (problem.Path, problem.Kind)).OrderBy(problem => problem.Path));
        Assert.Equal(["Mine", "Wheat"], _library.UsedBy("d3b.harvest").Select(asset => asset.Name));
    }

    [Fact]
    public async Task NothingIsWrittenUntilSomethingIsSaved()
    {
        await _library.LoadAsync([], TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(_library.Folder));
        Assert.Empty(_library.Assets);
    }

    [Fact]
    public async Task ASavedAssetIsAFileOfItsNameRenamedWithItAndDeletedWithIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _library.LoadAsync([Given()], cancellationToken);

        var version = _library.Version;
        var saved = await _library.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = " Ash ", Behavior = "d3b.harvest" }, cancellationToken);

        Assert.Equal(("Ash", "Ash.profile.json"), (saved.Name, saved.Path));
        Assert.True(File.Exists(Path.Combine(_library.Folder, "Ash.profile.json")));
        Assert.True(_library.Version > version);

        var renamed = await _library.SaveAsync(saved with { Name = "Ash and chestnut" }, cancellationToken);

        Assert.False(File.Exists(Path.Combine(_library.Folder, "Ash.profile.json")));
        Assert.True(File.Exists(Path.Combine(_library.Folder, "Ash and chestnut.profile.json")));
        Assert.Equal(saved.Id, renamed.Id);

        // Read again from the disk, the asset is the one saved.
        await _library.LoadAsync([Given()], cancellationToken);

        Assert.Equal(AssetFiles.Write(renamed), AssetFiles.Write(_library.Find(saved.Id)!));
        Assert.Equal(renamed.Path, _library.Find(saved.Id)?.Path);

        await _library.DeleteAsync(saved.Id, cancellationToken);

        Assert.Null(_library.Find(saved.Id));
        Assert.Empty(Directory.GetFiles(_library.Folder));
    }

    [Fact]
    public async Task AGivenAssetIsReadOnlyAndANameIsTakenOnceByKind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _library.LoadAsync([Given()], cancellationToken);
        await _library.SaveAsync(new ProfileAsset { Id = "p1", Name = "Ash", Behavior = "d3b.harvest" }, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _library.SaveAsync(Given() with { Title = null }, cancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _library.DeleteAsync("d3b.harvest", cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _library.SaveAsync(new ProfileAsset { Id = "p2", Name = "ASH", Behavior = "d3b.harvest" }, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _library.SaveAsync(new ProfileAsset { Id = "p3", Name = "a/b", Behavior = "d3b.harvest" }, cancellationToken));

        Assert.True(_library.IsTaken(AssetKind.Profile, "ash"));
        Assert.False(_library.IsTaken(AssetKind.Profile, "ash", "p1"));
        Assert.False(_library.IsTaken(AssetKind.Behavior, "Ash"));

        var behavior = await _library.SaveAsync(new BehaviorAsset { Id = "b1", Name = "Ash" }, cancellationToken);

        Assert.Equal("Ash.behavior.json", behavior.Path);
    }

    [Fact]
    public async Task FoldersAreListedEmptyOnesIncludedAndAnAssetIsSavedInOneAndMovedToAnother()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        WriteFile(Path.Combine("farm", "Wheat.profile.json"), """{"id":"p2","kind":"profile","format":1,"behavior":"d3b.harvest"}""");
        await _library.LoadAsync([Given()], cancellationToken);
        await _library.CreateFolderAsync("mines/astrub", cancellationToken);

        Assert.Equal(["farm", "mines", Path.Combine("mines", "astrub")], _library.Folders);

        var saved = await _library.SaveAsync(new ProfileAsset { Id = "p3", Name = "Iron", Behavior = "d3b.harvest" }, cancellationToken, Path.Combine("mines", "astrub"));

        Assert.Equal(Path.Combine("mines", "astrub", "Iron.profile.json"), saved.Path);

        var moved = await _library.MoveAsync("p3", "farm", cancellationToken);

        Assert.Equal(Path.Combine("farm", "Iron.profile.json"), moved.Path);
        Assert.True(File.Exists(Path.Combine(_library.Folder, "farm", "Iron.profile.json")));

        // Back at the root, then read again from the disk: the asset is where it was moved.
        await _library.MoveAsync("p3", "", cancellationToken);
        await _library.LoadAsync([Given()], cancellationToken);

        Assert.Equal("Iron.profile.json", _library.Find("p3")?.Path);
        await Assert.ThrowsAsync<ArgumentException>(() => _library.CreateFolderAsync("../out", cancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _library.MoveAsync("d3b.harvest", "farm", cancellationToken));
    }

    [Fact]
    public async Task AnImportedFileIsACopyUnderANewIdWhenItsOwnIsTakenAndAnExportIsTheProjectsFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _library.LoadAsync([Given()], cancellationToken);

        var saved = await _library.SaveAsync(new ProfileAsset { Id = "p1", Name = "Ash", Behavior = "d3b.harvest" }, cancellationToken);
        var exported = _directory.Combine("Elsewhere.profile.json");

        await _library.ExportAsync("p1", exported, cancellationToken);

        Assert.Equal(AssetFiles.Write(saved), File.ReadAllText(exported));

        var imported = await _library.ImportAsync(exported, "kept", cancellationToken);

        Assert.NotEqual("p1", imported.Id);
        Assert.Equal(("Elsewhere", Path.Combine("kept", "Elsewhere.profile.json")), (imported.Name, imported.Path));
        Assert.Equal(_library.Folder + Path.DirectorySeparatorChar + imported.Path, _library.FileOf(imported));
        Assert.Null(_library.FileOf(Given()));

        File.WriteAllText(_directory.Combine("notes.json"), "{}");
        await Assert.ThrowsAsync<AssetFormatException>(() => _library.ImportAsync(_directory.Combine("notes.json"), "", cancellationToken));
    }

    [Fact]
    public async Task AFolderIsRenamedItsAssetsFollowingAndDeletedWithThem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _library.LoadAsync([Given()], cancellationToken);
        await _library.SaveAsync(new ProfileAsset { Id = "p1", Name = "Iron", Behavior = "d3b.harvest" }, cancellationToken, Path.Combine("mines", "astrub"));
        await _library.SaveAsync(new ProfileAsset { Id = "p2", Name = "Ash", Behavior = "d3b.harvest" }, cancellationToken);

        var renamed = await _library.RenameFolderAsync("mines", "Mines", cancellationToken);

        Assert.Equal("Mines", renamed);
        Assert.Equal(Path.Combine("Mines", "astrub", "Iron.profile.json"), _library.Find("p1")?.Path);
        Assert.True(File.Exists(Path.Combine(_library.Folder, "Mines", "astrub", "Iron.profile.json")));
        Assert.Equal(["p1"], _library.AssetsIn("Mines").Select(asset => asset.Id));

        await _library.CreateFolderAsync("farm", cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _library.RenameFolderAsync("farm", "Mines", cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _library.RenameFolderAsync("farm", "a/b", cancellationToken));

        var deleted = await _library.DeleteFolderAsync("Mines", cancellationToken);

        Assert.Equal(["p1"], deleted.Select(asset => asset.Id));
        Assert.Null(_library.Find("p1"));
        Assert.NotNull(_library.Find("p2"));
        Assert.False(Directory.Exists(Path.Combine(_library.Folder, "Mines")));
        Assert.Equal(["farm"], _library.Folders);
        await Assert.ThrowsAsync<ArgumentException>(() => _library.DeleteFolderAsync("", cancellationToken));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Harvest", true)]
    [InlineData("Ash: north", false)]
    [InlineData("Ash.", false)]
    public void AnAssetsNameIsWhatAFileMayBeNamed(string name, bool valid)
    {
        Assert.Equal(valid, AssetLibrary.NameProblem(name) is null);
    }
}
