using System.Text;
using D3B.Bot.Reports;
using D3B.Data.Tests;
using D3B.Studio.Assets;

namespace D3B.Studio.Tests.Assets;

public sealed class ReportAssetsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly AssetLibrary _library;

    public ReportAssetsTests()
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
    public async Task TheProfileAndTheAssetsItPlaysAreTheReportsExceptWhatShipsWithD3B()
    {
        WriteFile("Wheat.profile.json", """{"id":"p1","kind":"profile","format":1,"behavior":"d3b.harvest","values":{"zone":"z1","note":"north","zones":["z2"]}}""");
        WriteFile("North.zone.json", """{"id":"z1","kind":"zone","format":1,"maps":[188744706]}""");
        WriteFile("South.zone.json", """{"id":"z2","kind":"zone","format":1,"maps":[188744707]}""");
        WriteFile("Elsewhere.zone.json", """{"id":"z3","kind":"zone","format":1,"maps":[1]}""");
        await _library.LoadAsync([Given()], TestContext.Current.CancellationToken);

        var parts = ReportAssets.Parts(_library, "p1");

        Assert.Equal(["assets/p1.json", "assets/z1.json", "assets/z2.json"], parts.Select(part => part.Name).Order(StringComparer.Ordinal));
        Assert.All(parts, part => Assert.True(ReportFiles.IsValid(part.Name)));
        Assert.Contains("\"behavior\": \"d3b.harvest\"", Encoding.UTF8.GetString(parts.Single(part => part.Name == "assets/p1.json").Data), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIdThatIsNoFileNameGetsOneOfItsOwnAndAnUnknownIdGivesNothing()
    {
        WriteFile("Odd.profile.json", """{"id":"my profile/1","kind":"profile","format":1,"behavior":"d3b.harvest"}""");
        await _library.LoadAsync([Given()], TestContext.Current.CancellationToken);

        var part = Assert.Single(ReportAssets.Parts(_library, "my profile/1"));

        Assert.True(ReportFiles.IsValid(part.Name), part.Name);
        Assert.Empty(ReportAssets.Parts(_library, "nobody"));
    }
}
