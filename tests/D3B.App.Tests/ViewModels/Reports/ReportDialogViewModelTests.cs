using System.Buffers;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Network;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.App.Tests.ViewModels.Reports;

public sealed class ReportDialogViewModelTests : IDisposable
{
    private static readonly ReportCharacterChoice _character = new(1, 11, "Aldebarane-du-Nord", "Level 80");

    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly Dialogs _dialogs = new();
    private readonly List<string> _copied = [];
    private readonly List<string> _opened = [];

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
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = "3.7.13.17", Listener = "l", ServerHost = "h", Server = "s" };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new CaptureWriter(File.Create(path), header, new FakeTimeProvider());

        writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new AuthenticationRequest { Ticket = "3f9a0c47be1d5528a60c9e4f71d3b8a2" }))));
        writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new CharacterListEvent { Characters = { new Character { Id = 11, Basics = new CharacterBasics { Name = _character.Name } } } }))));

        return path;
    }

    private ReportDialogViewModel Dialog(bool host = true, bool withCapture = true)
    {
        var capture = withCapture ? Capture() : null;

        return new ReportDialogViewModel(
            _localizer,
            _dialogs,
            [_character],
            choice => host
                ? new ReportSources { CharacterId = choice?.Id ?? 0, CharacterName = choice?.Name ?? "", CapturePath = capture, Translation = TestGame.Translation(), App = "0.8.1", Client = "3.7.13.17", Folder = _directory.Combine("reports") }
                : null,
            _copied.Add,
            _opened.Add);
    }

    [Fact]
    public void TheFormNeedsAnAreaAndForTheGameACharacter()
    {
        var dialog = Dialog();

        Assert.True(dialog.IsForm);
        Assert.False(dialog.NextCommand.CanExecute(null));

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.Fights);

        Assert.True(dialog.NextCommand.CanExecute(null));

        dialog.Character = null;

        Assert.False(dialog.NextCommand.CanExecute(null));

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

        Assert.True(dialog.NextCommand.CanExecute(null));
    }

    [Fact]
    public void TheMessageStopsAtTheLimitAndAPastedTextOverItIsRefusedWholeNotCut()
    {
        var dialog = Dialog();

        dialog.Message = new string('a', ReportContent.MessageLimit);

        Assert.Equal("1,500 / 1,500", dialog.MessageCount);

        dialog.Message = new string('b', ReportContent.MessageLimit + 1);

        Assert.Equal(new string('a', ReportContent.MessageLimit), dialog.Message);
    }

    [Fact]
    public async Task TheReportGoesFromTheFormToThePreviewToAFileWithItsCodeCopiedAndItsFolderOpened()
    {
        var dialog = Dialog();

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.Fights);
        dialog.Message = "The fight froze.";

        await dialog.NextCommand.ExecuteAsync(null);

        Assert.True(dialog.IsPreview);
        Assert.True(dialog.CanSave);
        Assert.Contains(dialog.Parts, part => part.IsRequired);
        Assert.Contains(dialog.Parts, part => part.Preview == "The fight froze.");

        dialog.SaveCommand.Execute(null);

        Assert.True(dialog.IsDone);
        Assert.Equal(dialog.Written!.Code, Assert.Single(_copied));
        Assert.Equal(_directory.Combine("reports"), Assert.Single(_opened));

        using var file = File.OpenRead(dialog.Written.Path);

        Assert.Equal("The fight froze.", ReportReader.Read(file).Message);
    }

    [Fact]
    public async Task APreviewWithoutTheCaptureOfTheGameSaysWhyAndCannotBeSaved()
    {
        var dialog = Dialog(withCapture: false);

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.Fights);

        await dialog.NextCommand.ExecuteAsync(null);

        Assert.True(dialog.IsPreview);
        Assert.True(dialog.HasProblem);
        Assert.False(dialog.CanSave);
        Assert.False(dialog.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task WithoutARunningHostTheFormSaysSoAndStays()
    {
        var dialog = Dialog(host: false);

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.Fights);

        await dialog.NextCommand.ExecuteAsync(null);

        Assert.True(dialog.IsForm);
        Assert.True(dialog.HasProblem);
    }

    [Fact]
    public async Task BackReturnsToTheFormAndCancelClosesTheDialog()
    {
        var dialog = Dialog();

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

        await dialog.NextCommand.ExecuteAsync(null);
        dialog.BackCommand.Execute(null);

        Assert.True(dialog.IsForm);
        Assert.Empty(dialog.Parts);

        _dialogs.Show(dialog);
        dialog.CancelCommand.Execute(null);

        Assert.False(_dialogs.IsOpen);
    }
}
