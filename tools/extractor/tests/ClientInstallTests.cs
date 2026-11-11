namespace D3B.Extractor.Tests;

public sealed class ClientInstallTests
{
    [Fact]
    public void TheNamedFolderComesBeforeTheEnvironment()
    {
        using var temporary = new TemporaryFolder();
        var named = temporary.Client("Version=3.7.13.17");

        var client = ClientInstall.Locate(named, _ => Path.Combine(temporary.Path, "elsewhere"));

        Assert.Equal(Path.GetFullPath(named), client.Root);
    }

    [Fact]
    public void TheEnvironmentNamesTheClientWhenNoFolderIs()
    {
        using var temporary = new TemporaryFolder();
        var named = temporary.Client("Version=3.7.13.17");

        var client = ClientInstall.Locate(null, variable => variable == ClientInstall.Variable ? named : null);

        Assert.Equal(Path.GetFullPath(named), client.Root);
    }

    [Fact]
    public void AFolderWithoutStreamingAssetsHoldsNoClient()
    {
        using var temporary = new TemporaryFolder();

        Assert.Throws<DirectoryNotFoundException>(() => ClientInstall.Locate(temporary.Path, _ => null));
    }
}
