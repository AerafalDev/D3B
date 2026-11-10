using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Network;
using D3B.Network.Captures;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Reports;

public sealed class ReportBundleTests
{
    private const string Code = "D3B-K7QM-4X";

    private static readonly DateTimeOffset _now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static CleanCapture Clean()
    {
        var header = new CaptureHeader { StartedAt = _now.AddMinutes(-30), ClientVersion = "3.7.13.17", Listener = "127.0.13.59:5556", ServerHost = "game.test", Server = "192.0.2.1:5555" };
        var time = new FakeTimeProvider();
        using var stream = new MemoryStream();

        using (var writer = new CaptureWriter(stream, header, time))
        {
            var frames = new[]
            {
                TestGame.Request(new AuthenticationRequest { Ticket = "3f9a0c47be1d5528a60c9e4f71d3b8a2", Language = "fr" }),
                TestGame.Event(new CharacterListEvent { Characters = { new Character { Id = 11, Basics = new CharacterBasics { Name = "Aldebarane", Level = 80 } } } }),
            };

            foreach (var (frame, index) in frames.Select((frame, index) => (frame, index)))
            {
                var framed = new System.Buffers.ArrayBufferWriter<byte>();

                D3B.Network.Framing.FrameWriter.Write(framed, frame);
                writer.Write(index is 0 ? CaptureRecordKind.FromClient : CaptureRecordKind.FromServer, new System.Buffers.ReadOnlySequence<byte>(framed.WrittenMemory));
            }
        }

        return new CaptureScrubber(TestGame.Translation()).Clean(new MemoryStream(stream.ToArray()));
    }

    private static ReportContent Content(ReportArea area = ReportArea.Fights, string? message = "The fight froze.", bool withCapture = true)
    {
        return new ReportContent
        {
            Code = Code,
            CreatedAt = _now,
            App = "0.8.1",
            Client = "3.7.13.17",
            Pack = "11",
            Mapping = "17",
            Area = area,
            Character = new ReportCharacter(11, 7, 80),
            Message = message,
            Capture = withCapture ? Clean() : null,
            Parts = [new ReportPart(ReportFiles.Journal, Encoding.UTF8.GetBytes("12:00:00 walk\n")), new ReportPart("assets/profile-1.json", "{}"u8.ToArray())],
        };
    }

    private static byte[] Written(ReportContent content)
    {
        using var stream = new MemoryStream();

        ReportWriter.Write(content, stream);

        return stream.ToArray();
    }

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

    [Fact]
    public void ACodeIsMadeOfLettersAndDigitsThatCannotBeMistakenForOneAnother()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var code = ReportCode.Create();

            Assert.True(ReportCode.IsValid(code), code);
            Assert.DoesNotContain(code[4..], character => character is 'O' or 'I' or '0' or '1');
        }

        Assert.False(ReportCode.IsValid("D3B-ABCD-0X"));
        Assert.False(ReportCode.IsValid("../../etc"));
        Assert.False(ReportCode.IsValid("D3B-K7QM-4X\n"));
        Assert.False(ReportCode.IsValid(null));
    }

    [Fact]
    public void AReportIsOneZipWithItsFilesAndAManifestThatListsThemWithTheirSizeAndSha256()
    {
        var content = Content();
        var bytes = Written(content);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var manifest = ReportReader.Read(new MemoryStream(bytes)).Manifest;

        Assert.Equal(ReportFiles.Manifest, zip.Entries[^1].FullName);
        Assert.Equal([ReportFiles.Message, ReportFiles.Capture, ReportFiles.Journal, "assets/profile-1.json", ReportFiles.Manifest], zip.Entries.Select(entry => entry.FullName));
        Assert.Equal(Code, manifest.Code);
        Assert.Equal((ReportArea.Fights, "3.7.13.17", "17", true), (manifest.Area, manifest.Client, manifest.Mapping, manifest.HasMessage));
        Assert.Equal(new ReportCharacter(11, 7, 80), manifest.Character);
        Assert.Equal(content.Capture!.StartedAt, manifest.CaptureStartedAt);

        var capture = manifest.Files.Single(file => file.Name == ReportFiles.Capture);

        Assert.Equal(content.Capture.Bytes.Length, capture.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content.Capture.Bytes)), capture.Sha256);
    }

    [Fact]
    public void AReportReadsBackWithTheFilesItWasWrittenWith()
    {
        var content = Content();
        var bundle = ReportReader.Read(new MemoryStream(Written(content)));

        Assert.Equal("The fight froze.", bundle.Message);
        Assert.Equal(content.Capture!.Bytes, bundle.Files[ReportFiles.Capture]);
        Assert.Equal("12:00:00 walk\n", Encoding.UTF8.GetString(bundle.Files[ReportFiles.Journal]));
    }

    [Fact]
    public void AReportWithoutAMessageIsAcceptedAndSaysSo()
    {
        foreach (var message in new string?[] { null, "" })
        {
            var bundle = ReportReader.Read(new MemoryStream(Written(Content(message: message))));

            Assert.False(bundle.Manifest.HasMessage);
            Assert.Null(bundle.Message);
            Assert.DoesNotContain(ReportFiles.Message, bundle.Files.Keys);
        }
    }

    [Fact]
    public void AMessageOfMoreThan1500CharactersIsRefusedAndOneOf1500IsKept()
    {
        Assert.Throws<ArgumentException>(() => Written(Content(message: new string('a', ReportContent.MessageLimit + 1))));

        var bundle = ReportReader.Read(new MemoryStream(Written(Content(message: new string('é', ReportContent.MessageLimit)))));

        Assert.Equal(ReportContent.MessageLimit, bundle.Message!.Length);
    }

    [Fact]
    public void AReportOfTheGameCannotGoWithoutItsCaptureButAReportOfTheAppCan()
    {
        foreach (var area in new[] { ReportArea.Fights, ReportArea.HarvestAndBank, ReportArea.MovingAndMaps, ReportArea.NpcsAndDialogs })
            Assert.Throws<ArgumentException>(() => Written(Content(area, withCapture: false)));

        var bundle = ReportReader.Read(new MemoryStream(Written(Content(ReportArea.App, withCapture: false))));

        Assert.Null(bundle.Manifest.CaptureStartedAt);
        Assert.DoesNotContain(ReportFiles.Capture, bundle.Files.Keys);
    }

    [Fact]
    public void ThePlayerCanUncheckEveryPartButTheCaptureOfTheGame()
    {
        var content = Content() with { Message = null, Parts = [] };
        var bundle = ReportReader.Read(new MemoryStream(Written(content)));

        Assert.Equal([ReportFiles.Capture], bundle.Files.Keys);
    }

    [Theory]
    [InlineData("../evil.json")]
    [InlineData("assets/../../evil.json")]
    [InlineData("C:/evil.json")]
    [InlineData("notes.txt")]
    [InlineData("manifest.json")]
    [InlineData("capture.d3bcap")]
    public void APartWithAnotherNameThanAReportsIsRefused(string name)
    {
        var content = Content() with { Parts = [new ReportPart(name, [1])] };

        Assert.Throws<ArgumentException>(() => Written(content));
    }

    [Fact]
    public void AReaderRefusesAnEntryWhoseNameCouldLeaveTheFolderItIsUnpackedIn()
    {
        var good = Written(Content());

        foreach (var name in new[] { "../evil.json", "assets/../../evil.json", "/etc/passwd", "C:\\evil.json", "notes.txt", "assets/x.exe" })
        {
            using var source = new ZipArchive(new MemoryStream(good));
            var entries = source.Entries.Select(entry => (entry.FullName, Read(entry))).Append((name, [1])).ToArray();

            Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip(entries))));
        }

        static byte[] Read(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var memory = new MemoryStream();

            stream.CopyTo(memory);

            return memory.ToArray();
        }
    }

    [Fact]
    public void AReaderRefusesAZipOfTooManyEntriesOrOneThatExpandsPastTheBound()
    {
        var many = Zip([.. Enumerable.Range(0, 5).Select(index => ($"assets/a{index}.json", (byte[])[1]))]);

        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(many), new ReportLimits { MaxEntries = 4 }));

        // 8 MiB of zeros is a few KiB zipped: the bound is on what it expands to, not on what was posted.
        var bomb = Zip((ReportFiles.Capture, new byte[8 * 1024 * 1024]));
        var exception = Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(bomb), new ReportLimits { MaxEntryBytes = 1024 * 1024 }));

        Assert.True(bomb.Length < 100 * 1024);
        Assert.Contains("expands past", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip((ReportFiles.Journal, new byte[600]), (ReportFiles.Trace, new byte[600]))), new ReportLimits { MaxTotalBytes = 1000 }));
    }

    [Fact]
    public void AReaderRefusesAFileThatIsNotTheOneTheManifestListsOrOneItDoesNotList()
    {
        var written = Written(Content());
        var manifest = ReportReader.Read(new MemoryStream(written)).Manifest;

        using var source = new ZipArchive(new MemoryStream(written));
        using var manifestStream = source.GetEntry(ReportFiles.Manifest)!.Open();
        using var manifestMemory = new MemoryStream();

        manifestStream.CopyTo(manifestMemory);

        var manifestBytes = manifestMemory.ToArray();
        var files = manifest.Files.Select(file => (file.Name, Data: file.Name == ReportFiles.Message ? Encoding.UTF8.GetBytes("The fight froze.") : file.Name == ReportFiles.Journal ? Encoding.UTF8.GetBytes("12:00:00 walk\n") : file.Name == "assets/profile-1.json" ? "{}"u8.ToArray() : Clean().Bytes)).ToArray();

        // The capture rebuilt here is the same bytes, so the manifest holds: the control.
        Assert.Equal(Code, ReportReader.Read(new MemoryStream(Zip([.. files, (ReportFiles.Manifest, manifestBytes)]))).Manifest.Code);

        // One byte changed.
        var changed = files.Select(file => file.Name == ReportFiles.Journal ? (file.Name, Data: Encoding.UTF8.GetBytes("12:00:00 WALK\n")) : file).ToArray();

        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip([.. changed, (ReportFiles.Manifest, manifestBytes)]))));

        // A file the manifest does not list.
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip([.. files, (ReportFiles.Trace, [1]), (ReportFiles.Manifest, manifestBytes)]))));

        // A file it lists and the report lacks.
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip([.. files.Where(file => file.Name != ReportFiles.Journal), (ReportFiles.Manifest, manifestBytes)]))));

        // No manifest, or one that is no JSON.
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip(files))));
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream(Zip([.. files, (ReportFiles.Manifest, "not json"u8.ToArray())]))));
    }

    [Fact]
    public void AFileThatIsNoZipIsRefusedNotCrashedOn()
    {
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream("MZ this is not a report"u8.ToArray())));
        Assert.Throws<ReportFormatException>(() => ReportReader.Read(new MemoryStream([])));
    }

    [Fact]
    public void AReportIsUnpackedInAFolderWithItsManifestAndNothingOutsideIt()
    {
        using var directory = new D3B.Data.Tests.TemporaryDirectory();
        var bundle = ReportReader.Read(new MemoryStream(Written(Content())));
        var target = directory.Combine("reports", Code);

        bundle.WriteTo(target);

        var unpacked = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(target, path).Replace('\\', '/')).Order(StringComparer.Ordinal);

        Assert.Equal(new[] { ReportFiles.Capture, ReportFiles.Journal, ReportFiles.Manifest, ReportFiles.Message, "assets/profile-1.json" }.Order(StringComparer.Ordinal), unpacked);
        Assert.Equal(["reports"], Directory.GetDirectories(directory.Path).Select(Path.GetFileName));
    }
}
