using System.Buffers;
using System.Text;
using D3B.App.Localization;
using D3B.App.ViewModels.Reports;
using D3B.Bot.Journal;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Network;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.App.Tests.ViewModels.Reports;

public sealed class ReportDraftTests : IDisposable
{
    private const string Ticket = "3f9a0c47be1d5528a60c9e4f71d3b8a2";
    private const string Name = "Aldebarane-du-Nord";

    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static byte[] Framed(byte[] frame)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, frame);

        return writer.WrittenSpan.ToArray();
    }

    private string Capture()
    {
        var path = _directory.Combine("captures", "game", "20261110-090000-0HNQ1AAA.d3bcap");
        var header = new CaptureHeader { StartedAt = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), ClientVersion = "3.7.13.17", Listener = "127.0.13.59:5556", ServerHost = "game.test", Server = "192.0.2.1:5555" };
        var character = new Character { Id = 11, Basics = new CharacterBasics { Name = Name, Level = 80 } };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var writer = new CaptureWriter(File.Create(path), header, new FakeTimeProvider()))
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new AuthenticationRequest { Ticket = Ticket, Language = "fr" }))));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new CharacterListEvent { Characters = { character } }))));
        }

        return path;
    }

    private ReportSources Sources(string? capturePath, Func<CancellationToken, Task<ReportPart>>? knowledge = null)
    {
        return new ReportSources
        {
            CharacterId = 11,
            CharacterName = Name,
            Level = 80,
            Breed = 7,
            CapturePath = capturePath,
            Translation = TestGame.Translation(),
            Journal = [new NoteEntry(new DateTimeOffset(2026, 11, 10, 9, 5, 0, TimeSpan.Zero), "harvest", Name + " reached the bank")],
            Assets = [new ReportPart("assets/p1.json", "{}"u8.ToArray())],
            Knowledge = knowledge,
            Settings = new Dictionary<string, string?> { ["Delays:Display"] = "1.5" },
            App = "0.8.1",
            Client = "3.7.13.17",
            Pack = "11",
            Mapping = "17",
            Folder = _directory.Combine("reports"),
            Time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero)),
        };
    }

    private Task<ReportDraft> PrepareAsync(ReportSources sources, ReportArea area = ReportArea.Fights, string? message = null)
    {
        return ReportDraft.PrepareAsync(sources, area, message, _localizer, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AReportOfTheGameCarriesItsCaptureCleanedAndNothingOfTheTicketOrTheName()
    {
        var draft = await PrepareAsync(Sources(Capture()), message: "The fight froze.");
        var written = draft.Write();
        var bytes = File.ReadAllBytes(written.Path);
        var bundle = ReportReader.Read(new MemoryStream(bytes));

        Assert.Equal(Path.Combine(_directory.Combine("reports"), draft.Code + ".d3breport"), written.Path);
        Assert.Equal(draft.Code, bundle.Manifest.Code);
        Assert.Equal(new ReportCharacter(11, 7, 80), bundle.Manifest.Character);
        Assert.Equal("The fight froze.", bundle.Message);
        Assert.Contains(ReportFiles.Capture, bundle.Files.Keys);

        foreach (var file in bundle.Files.Values)
        {
            Assert.False(file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Ticket)) >= 0);
            Assert.False(file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Name)) >= 0);
        }
    }

    [Fact]
    public async Task EachPartButTheRequiredCaptureCanBeUncheckedAndLeavesTheReport()
    {
        var draft = await PrepareAsync(Sources(Capture()), message: "Hello");

        draft.Capture!.IsIncluded = false;

        Assert.True(draft.Capture.IsIncluded);
        Assert.False(draft.Capture.CanChange);

        foreach (var part in draft.Parts.Where(part => part.CanChange))
            part.IsIncluded = false;

        var bundle = ReportReader.Read(new MemoryStream(File.ReadAllBytes(draft.Write().Path)));

        Assert.Equal([ReportFiles.Capture], bundle.Files.Keys);
        Assert.False(bundle.Manifest.HasMessage);
    }

    [Fact]
    public async Task TheJournalIsShownAsItGoesWithTheCharactersNameItsNumber()
    {
        var draft = await PrepareAsync(Sources(Capture()));
        var journal = draft.Parts.Single(part => part.Files.Any(file => file.Name == ReportFiles.Journal));

        Assert.Contains("character 11 reached the bank", journal.Preview, StringComparison.Ordinal);
        Assert.DoesNotContain(Name, journal.Preview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportOfTheGameWithoutACaptureSaysWhyAndCannotBeWritten()
    {
        foreach (var sources in new[] { Sources(null), Sources(_directory.Combine("captures", "game", "gone.d3bcap")) with { CapturePath = null }, Sources(Capture()) with { Translation = null } })
        {
            var draft = await PrepareAsync(sources);

            Assert.Null(draft.Capture);
            Assert.NotNull(draft.CaptureProblem);
            Assert.False(draft.CanWrite);
            Assert.Throws<InvalidOperationException>(() => draft.Write());
        }
    }

    [Fact]
    public async Task ACaptureTheCleanerRefusesIsNotCarriedInstead()
    {
        var path = _directory.Combine("captures", "game", "20261110-090000-0HNQ1ZZZ.d3bcap");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // A capture with a ticket and no character list: nothing to hide the names with.
        using (var writer = new CaptureWriter(File.Create(path), new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = "3.7.13.17", Listener = "l", ServerHost = "h", Server = "s" }, new FakeTimeProvider()))
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new AuthenticationRequest { Ticket = Ticket }))));

        var draft = await PrepareAsync(Sources(path));

        Assert.Null(draft.Capture);
        Assert.False(draft.CanWrite);
        Assert.Contains("character list", draft.CaptureProblem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportOfTheAppNeedsNoCaptureAndHoldsNone()
    {
        var draft = await PrepareAsync(Sources(null), ReportArea.App);
        var bundle = ReportReader.Read(new MemoryStream(File.ReadAllBytes(draft.Write().Path)));

        Assert.True(draft.CanWrite);
        Assert.Null(draft.Capture);
        Assert.DoesNotContain(ReportFiles.Capture, bundle.Files.Keys);
    }

    [Fact]
    public async Task WhatTheBotsLearnedIsAPartOfItsOwnWhenThereIsADatabase()
    {
        var learned = new ReportPart(ReportFiles.Knowledge, "{}"u8.ToArray());
        var draft = await PrepareAsync(Sources(Capture(), _ => Task.FromResult(learned)));
        var part = draft.Parts.Single(part => part.Files.Contains(learned));

        part.IsIncluded = false;

        Assert.DoesNotContain(ReportFiles.Knowledge, ReportReader.Read(new MemoryStream(File.ReadAllBytes(draft.Write().Path))).Files.Keys);
        Assert.DoesNotContain(ReportFiles.Knowledge, (await PrepareAsync(Sources(Capture()))).Parts.SelectMany(part => part.Files).Select(file => file.Name));
    }

    [Fact]
    public async Task TheCaptureOfASessionStillBeingRecordedIsCleanedWhileTheRelayHoldsItOpen()
    {
        var path = _directory.Combine("captures", "game", "20261110-090000-0HNQ1OPEN.d3bcap");
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = "3.7.13.17", Listener = "l", ServerHost = "h", Server = "s" };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // The relay opens a capture to write, sharing it for reading, and the player reports while it records.
        using var writer = new CaptureWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), header, new FakeTimeProvider());

        writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new AuthenticationRequest { Ticket = Ticket }))));
        writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new CharacterListEvent { Characters = { new Character { Id = 11, Basics = new CharacterBasics { Name = Name } } } }))));

        var draft = await PrepareAsync(Sources(path));

        Assert.NotNull(draft.Capture);
        Assert.True(draft.CanWrite);
    }
}
