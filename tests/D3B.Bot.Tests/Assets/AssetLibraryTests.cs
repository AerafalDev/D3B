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
        Assert.Equal(["Broken.profile.json", "Other.profile.json"], _library.Problems.Select(problem => problem.Path).Order());
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
