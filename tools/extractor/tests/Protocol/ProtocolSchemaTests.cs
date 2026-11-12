using System.Text;
using System.Text.Json;
using D3B.Extractor.Protocol;

namespace D3B.Extractor.Tests.Protocol;

public sealed class ProtocolSchemaTests
{
    private static readonly ProtocolSchema _schema = new()
    {
        Messages =
        [
            new SchemaMessage
            {
                Name = "abc",
                Fields =
                [
                    new SchemaField { Number = 1, Kind = "int64", Label = "optional" },
                    new SchemaField { Number = 2, Kind = "map", Key = "int32", Value = "enum", Type = "abc.abe" },
                    new SchemaField { Number = 3, Kind = "message", Type = "abd", Oneof = "abc.abf" },
                ],
            },
            new SchemaMessage { Name = "abd", Fields = [] },
        ],
        Enums = [new SchemaEnum { Name = "abc.abe", Values = [0, 2] }],
    };

    [Fact]
    public void TheCanonicalFormIsCompactWithItsKeysSortedAndNoBuild()
    {
        var canonical = Encoding.UTF8.GetString(_schema.Canonical());

        Assert.Equal(
            """{"enums":[{"name":"abc.abe","values":[0,2]}],"format":1,"messages":[{"fields":[{"kind":"int64","label":"optional","number":1},{"key":"int32","kind":"map","number":2,"type":"abc.abe","value":"enum"},{"kind":"message","number":3,"oneof":"abc.abf","type":"abd"}],"name":"abc"},{"fields":[],"name":"abd"}]}""",
            canonical);
    }

    [Fact]
    public void TheDigestIsTheSha256OfTheCanonicalForm()
    {
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(_schema.Canonical())), _schema.Digest());
        Assert.Matches("^[0-9a-f]{64}$", _schema.Digest());
    }

    [Fact]
    public void TheDigestChangesWithAFieldNumber()
    {
        var other = new ProtocolSchema
        {
            Messages = [new SchemaMessage { Name = "abd", Fields = [new SchemaField { Number = 2, Kind = "int32" }] }],
            Enums = [],
        };
        var renumbered = new ProtocolSchema
        {
            Messages = [new SchemaMessage { Name = "abd", Fields = [new SchemaField { Number = 3, Kind = "int32" }] }],
            Enums = [],
        };

        Assert.NotEqual(other.Digest(), renumbered.Digest());
    }

    [Fact]
    public void TheFileNamesItsFormatBuildAndDigestAroundTheCanonicalForm()
    {
        using var file = JsonDocument.Parse(_schema.File("3.7.13.17"));
        var root = file.RootElement;

        Assert.Equal(["build", "enums", "format", "messages", "sha256"], root.EnumerateObject().Select(member => member.Name));
        Assert.Equal("3.7.13.17", root.GetProperty("build").GetString());
        Assert.Equal(ProtocolSchema.Format, root.GetProperty("format").GetInt32());
        Assert.Equal(_schema.Digest(), root.GetProperty("sha256").GetString());
        Assert.Equal(3, root.GetProperty("messages")[0].GetProperty("fields").GetArrayLength());
    }

    [Fact]
    public void TwoBuildsOfOneSchemaShareTheirDigest()
    {
        using var first = JsonDocument.Parse(_schema.File("3.7.13.17"));
        using var second = JsonDocument.Parse(_schema.File("3.7.14.2"));

        Assert.Equal(first.RootElement.GetProperty("sha256").GetString(), second.RootElement.GetProperty("sha256").GetString());
    }

    [Fact]
    public void WritingReplacesAnEarlierFileWhole()
    {
        using var temporary = new TemporaryFolder();
        var path = Path.Combine(temporary.Path, "protocol", "3.7.13.17.json");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "earlier, and longer than the schema written over it: earlier, and longer, and longer again");
        _schema.WriteTo(path, "3.7.13.17");

        Assert.Equal(_schema.File("3.7.13.17"), File.ReadAllBytes(path));
        Assert.Equal([Path.GetFileName(path)], Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }
}
