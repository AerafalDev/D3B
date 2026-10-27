using D3B.Data;

namespace D3B.Scripts.Schema;

/// <summary>Where a client build's obfuscated schema is kept: beside the build's pack, one file per schema format.</summary>
/// <remarks>
/// The schema is a <c>FileDescriptorSet</c>, protobuf's own description of protobuf files, serialized. It is derived from
/// the client, so it is never committed: it ships as an asset of the build's release, as packs do (D24).
/// </remarks>
internal static class SchemaFile
{
    /// <summary>The format this tool writes. It changes whenever the tool would describe a client published before otherwise.</summary>
    public const int FormatVersion = 1;

    /// <summary>Gets the file name of a schema of <see cref="FormatVersion"/>.</summary>
    public static string FileName => $"schema-{FormatVersion}.binpb";

    /// <summary>Gets the path of the schema of a client build under a root directory, such as where packs are kept.</summary>
    public static string PathOf(string root, ClientBuild build)
    {
        return Path.Combine(root, build.Version, FileName);
    }
}
