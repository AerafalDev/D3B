namespace D3B.Data.Tests;

public sealed class ClientLocatorTests
{
    [Fact]
    public void TheLauncherNamesWhereItInstalledTheClient()
    {
        using var directory = new TemporaryDirectory();
        var client = InstalledClient(directory, "Dofus-dofus3");
        var release = Release(directory, client);

        Assert.Equal(client, ClientLocator.FromLauncher(release));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"name\":\"dofus3\"}")]
    [InlineData("{\"location\":42}")]
    [InlineData("{\"location\":\"\"}")]
    public void AReleaseFileNamingNoLocationNamesNoClient(string text)
    {
        using var directory = new TemporaryDirectory();
        var release = directory.Combine("release.json");

        File.WriteAllText(release, text);

        Assert.Null(ClientLocator.FromLauncher(release));
    }

    [Fact]
    public void NoReleaseFileNamesNoClient()
    {
        using var directory = new TemporaryDirectory();

        Assert.Null(ClientLocator.FromLauncher(directory.Combine("release.json")));
    }

    [Fact]
    public void TheConfiguredDirectoryWinsOverTheLauncher()
    {
        using var directory = new TemporaryDirectory();
        var release = Release(directory, InstalledClient(directory, "Dofus-dofus3"));
        var configured = directory.Combine("Elsewhere");

        Assert.Equal(configured, ClientLocator.Locate(configured, release, directory.Combine("Default")));
    }

    [Fact]
    public void WithoutAConfiguredDirectoryTheLauncherIsAsked()
    {
        using var directory = new TemporaryDirectory();
        var client = InstalledClient(directory, "D", "Games", "Dofus-dofus3");
        var release = Release(directory, client);

        Assert.Equal(client, ClientLocator.Locate(null, release, InstalledClient(directory, "Default")));
    }

    [Fact]
    public void ALauncherLocationHoldingNoClientIsPassedOver()
    {
        using var directory = new TemporaryDirectory();
        var release = Release(directory, directory.Combine("Uninstalled"));
        var fallback = directory.Combine("Default");

        Assert.Equal(fallback, ClientLocator.Locate(" ", release, fallback));
    }

    [Fact]
    public void WithoutTheLauncherTheDefaultDirectoryIsUsed()
    {
        using var directory = new TemporaryDirectory();
        var fallback = directory.Combine("Default");

        Assert.Equal(fallback, ClientLocator.Locate(null, directory.Combine("release.json"), fallback));
    }

    private static string InstalledClient(TemporaryDirectory directory, params string[] parts)
    {
        var client = directory.Combine(parts);
        var streamingAssets = Path.Combine(client, "Dofus_Data", "StreamingAssets");

        Directory.CreateDirectory(streamingAssets);
        File.WriteAllText(Path.Combine(streamingAssets, "version"), "Version=3.7.14.18\n");

        return client;
    }

    private static string Release(TemporaryDirectory directory, string location)
    {
        var release = directory.Combine("release.json");
        var escaped = location.Replace(@"\", @"\\", StringComparison.Ordinal);

        File.WriteAllText(release, $"{{\"name\":\"dofus3\",\"location\":\"{escaped}\",\"isUpdating\":false}}");

        return release;
    }
}
