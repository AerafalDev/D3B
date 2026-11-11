namespace D3B.Extractor.Tests;

public sealed class ClientBuildTests
{
    [Fact]
    public void AVersionFileGivesTheVersionAndTheBuildDate()
    {
        var build = ClientBuild.Parse("Version=3.7.13.17\r\nBuildDate=2026-11-03T14:09:52+00:00\r\nConfigUrl=https://example.com/config.json\r\n");

        Assert.Equal("3.7.13.17", build.Version);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 14, 9, 52, TimeSpan.Zero), build.BuildDate);
    }

    [Fact]
    public void ABuildDateIsOptional()
    {
        Assert.Null(ClientBuild.Parse("Version=3.7.13.17").BuildDate);
    }

    [Theory]
    [InlineData("Version=3.7.x.17")]
    [InlineData("Version=../../evil")]
    [InlineData("Version=3..7")]
    [InlineData("Version=")]
    [InlineData("BuildDate=2026-11-03")]
    [InlineData("Version=3.7.13.17\nBuildDate=yesterday")]
    [InlineData("Version=3.7.13.17\nBuildDate=11/03/2026")]
    [InlineData("Version=3.7.13.17\nBuildDate=2026-11-03T14:09:52")]
    [InlineData("Version=3.7.13.17\nVersion=3.7.13.18")]
    [InlineData("Version=1234567890.1234567890.1234567890.1234567890.1234567890.1234567890")]
    public void AVersionThatIsNotNumbersSeparatedByDotsIsRefused(string text)
    {
        Assert.Throws<InvalidDataException>(() => ClientBuild.Parse(text));
    }

    [Fact]
    public void AFileTooLargeForAVersionFileIsRefused()
    {
        using var temporary = new TemporaryFolder();
        var root = temporary.Client("Version=3.7.13.17\n" + new string('#', 5000));

        Assert.Throws<InvalidDataException>(() => ClientBuild.Read(ClientInstall.Locate(root)));
    }

    [Fact]
    public void AClientWithoutVersionFileIsRefused()
    {
        using var temporary = new TemporaryFolder();
        var root = temporary.Client("Version=3.7.13.17");

        File.Delete(Path.Combine(root, "Dofus_Data", "StreamingAssets", "version"));

        Assert.Throws<InvalidDataException>(() => ClientBuild.Read(ClientInstall.Locate(root)));
    }
}
