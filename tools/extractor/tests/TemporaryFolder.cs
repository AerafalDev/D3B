namespace D3B.Extractor.Tests;

/// <summary>A folder of its own for one test, deleted after it.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d3b-extractor-" + Guid.NewGuid().ToString("N"));

    public TemporaryFolder()
    {
        Directory.CreateDirectory(Path);
    }

    /// <summary>A fake client install holding only its version file.</summary>
    public string Client(string version)
    {
        var root = System.IO.Path.Combine(Path, "client");
        var streamingAssets = System.IO.Path.Combine(root, "Dofus_Data", "StreamingAssets");

        Directory.CreateDirectory(streamingAssets);
        File.WriteAllText(System.IO.Path.Combine(streamingAssets, "version"), version);

        return root;
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
