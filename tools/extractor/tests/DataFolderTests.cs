using System.Text;

namespace D3B.Extractor.Tests;

public sealed class DataFolderTests
{
    [Fact]
    public void AFolderWithoutTablesHoldsTheManifestTheAppReads()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "3.7.13.17");

        DataFolder.Create(target).Complete("3.7.13.17", "0.1.0");

        // The app's own tests read the same fixture (src-tauri/src/data/manifest.rs).
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "empty", "manifest.json"));

        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(target, DataFolder.ManifestFile)));
    }

    [Fact]
    public void EachFileIsListedWithItsSha256InOrderOfItsPath()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");
        var folder = DataFolder.Create(target);

        folder.Write("maps/7.json", Encoding.UTF8.GetBytes("[]\n"));
        folder.Write("areas.json", Encoding.UTF8.GetBytes("abc"));
        folder.Complete("3.7.13.17", "0.1.0");

        var manifest = File.ReadAllText(Path.Combine(target, DataFolder.ManifestFile));

        Assert.Contains("""
            {
                  "name": "areas",
                  "path": "areas.json",
                  "sha256": "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
                }
            """.Replace("\r\n", "\n", StringComparison.Ordinal), manifest, StringComparison.Ordinal);
        Assert.True(manifest.IndexOf("areas.json", StringComparison.Ordinal) < manifest.IndexOf("maps/7.json", StringComparison.Ordinal));
        Assert.Equal("[]\n", File.ReadAllText(Path.Combine(target, "maps", "7.json")));
    }

    [Fact]
    public void ACompleteFolderReplacesTheOneBefore()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");
        var first = DataFolder.Create(target);

        first.Write("old.json", "[]"u8);
        first.Complete("3.7.13.17", "0.1.0");
        DataFolder.Create(target).Complete("3.7.13.18", "0.1.0");

        Assert.False(File.Exists(Path.Combine(target, "old.json")));
        Assert.Equal(["data"], Directory.GetDirectories(temporary.Path).Select(Path.GetFileName));
        Assert.Contains("3.7.13.18", File.ReadAllText(Path.Combine(target, DataFolder.ManifestFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderThatIsNotADataFolderIsNeverReplaced()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "documents");

        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "notes.txt"), "mine");

        Assert.Throws<IOException>(() => DataFolder.Create(target));
        Assert.True(File.Exists(Path.Combine(target, "notes.txt")));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("/rooted.json")]
    [InlineData("maps//7.json")]
    [InlineData(@"maps\7.json")]
    [InlineData("C:/rooted.json")]
    [InlineData("manifest.json")]
    [InlineData("maps./7.json")]
    [InlineData("maps /7.json")]
    [InlineData("maps/7?.json")]
    public void APathOutsideTheFolderIsRefused(string path)
    {
        using var temporary = new TemporaryFolder();
        var folder = DataFolder.Create(Path.Combine(temporary.Path, "data"));

        Assert.Throws<ArgumentException>(() => folder.Write(path, "[]"u8));
    }

    [Theory]
    [InlineData("areas.json")]
    [InlineData("Areas.json")]
    [InlineData("areas.jpg")]
    public void APathOrNameIsWrittenOnceWhateverItsCase(string second)
    {
        using var temporary = new TemporaryFolder();
        var folder = DataFolder.Create(Path.Combine(temporary.Path, "data"));

        folder.Write("areas.json", "[]"u8);

        Assert.Throws<ArgumentException>(() => folder.Write(second, "[]"u8));
    }

    [Fact]
    public void ATrailingSeparatorNamesTheSameFolder()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");

        DataFolder.Create(target + Path.DirectorySeparatorChar).Complete("3.7.13.17", "0.1.0");
        DataFolder.Create(target + "/").Complete("3.7.13.18", "0.1.0");

        Assert.Equal([DataFolder.ManifestFile], Directory.GetFileSystemEntries(target).Select(Path.GetFileName));
        Assert.Contains("3.7.13.18", File.ReadAllText(Path.Combine(target, DataFolder.ManifestFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void WhatACrashedRunLeftIsNotCarriedIntoTheNextFolder()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");
        var crashed = DataFolder.Create(target);

        crashed.Write("stale.json", "[]"u8);
        DataFolder.Create(target).Complete("3.7.13.17", "0.1.0");

        Assert.False(File.Exists(Path.Combine(target, "stale.json")));
    }

    [Fact]
    public void ATargetThatStoppedBeingADataFolderIsNotReplaced()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");
        var folder = DataFolder.Create(target);

        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "notes.txt"), "mine");

        Assert.Throws<IOException>(() => folder.Complete("3.7.13.17", "0.1.0"));
        Assert.True(File.Exists(Path.Combine(target, "notes.txt")));
    }
}
