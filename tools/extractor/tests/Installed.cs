namespace D3B.Extractor.Tests;

/// <summary>The installed client, for the tests that measure the real one: they are skipped where there is none, as in CI.</summary>
internal static class Installed
{
    public static ClientInstall Client()
    {
        try
        {
            return ClientInstall.Locate(null);
        }
        catch (DirectoryNotFoundException exception)
        {
            Assert.Skip($"No installed client: {exception.Message}");

            throw;
        }
    }

    /// <summary>The installed client if it is of <paramref name="build"/>, for the tests that measure that build's counts.</summary>
    public static ClientInstall Client(string build)
    {
        var client = Client();
        var installed = ClientBuild.Read(client).Version;

        if (installed != build)
        {
            Assert.Skip($"The installed client is {installed}, not {build}.");
        }

        return client;
    }
}
