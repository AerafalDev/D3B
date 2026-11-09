namespace D3B.Hooks;

/// <summary>
/// The writes Claude may not make in the project: what is extracted from the official client is never committed, and the
/// references in <c>vendor/</c> are read, never changed (D3, D10, D11, D24).
/// </summary>
internal static class WriteGuard
{
    private const string ProtocolFolder = "src/D3B.Protocol/";
    private const string TestsFolder = "tests/";

    private static readonly HashSet<string> _extractedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pack",
        ".binpb",
        ".sha256",
        ".d3bcap",
        ".db",
        ".db-wal",
        ".db-shm",
        ".sqlite",
        ".bundle",
        ".unity3d",
        ".ress",
        ".assets",
        ".bank",
    };

    /// <summary>Why Claude may not write <paramref name="path"/>, relative to the project, or <see langword="null"/> when it may.</summary>
    public static string? Refusal(string path)
    {
        if (path.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (path.StartsWith("vendor/", StringComparison.OrdinalIgnoreCase))
        {
            return "vendor/ holds the references D3B reads for ideas, never copies and never changes (D3).";
        }

        var extension = Path.GetExtension(path);
        var isExtracted = _extractedExtensions.Contains(extension)
            || Path.GetFileName(path).Equals("global-metadata.dat", StringComparison.OrdinalIgnoreCase);
        if (isExtracted)
        {
            return $"{path} is data from the official client or from a machine: a pack, a schema, a mapping, a capture or a database. None is ever committed (D10, D11, D24); packs and mappings ship as release assets.";
        }

        var isOurs = path.StartsWith(ProtocolFolder, StringComparison.OrdinalIgnoreCase) || path.StartsWith(TestsFolder, StringComparison.OrdinalIgnoreCase);
        if (extension.Equals(".proto", StringComparison.OrdinalIgnoreCase) && !isOurs)
        {
            return $".proto text outside {ProtocolFolder} is what scripts/schema.cs writes from the client's schema, which is never committed (D11). The readable .proto files are ours, in {ProtocolFolder}, and the tests' fixtures in {TestsFolder}.";
        }

        return null;
    }
}
