using D3B.Bot.Reports;
using D3B.Data.Tests;

namespace D3B.Bot.Tests.Reports;

public sealed class ReportCapturesTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private string Write(string name)
    {
        var path = _directory.Combine(name);

        File.WriteAllBytes(path, [1]);

        return path;
    }

    [Fact]
    public void ACaptureIsFoundByTheIdOfItsConnectionWhichEndsItsName()
    {
        Write("20261110-090000-0HNQ1AAA.d3bcap");

        var wanted = Write("20261110-091500-0HNQ1BBB.d3bcap");

        Write("20261110-093000-0HNQ1CCC.d3bcap");

        Assert.Equal(wanted, ReportCaptures.Find(_directory.Path, "0HNQ1BBB"));
    }

    [Theory]
    [InlineData("0HNQ1")]
    [InlineData("")]
    [InlineData("../0HNQ1BBB")]
    [InlineData("*")]
    public void AnIdThatIsNoWholeConnectionIdFindsNothing(string id)
    {
        Write("20261110-091500-0HNQ1BBB.d3bcap");

        Assert.Null(ReportCaptures.Find(_directory.Path, id));
    }

    [Fact]
    public void NothingIsFoundInAMissingFolderOrForNoId()
    {
        Assert.Null(ReportCaptures.Find(_directory.Combine("never-made"), "0HNQ1BBB"));
        Assert.Null(ReportCaptures.Find(_directory.Path, null));
    }
}
