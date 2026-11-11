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
}
