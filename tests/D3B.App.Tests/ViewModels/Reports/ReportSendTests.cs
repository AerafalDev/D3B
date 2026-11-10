using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.Bot.Reports;
using D3B.Data.Tests;

namespace D3B.App.Tests.ViewModels.Reports;

public sealed class ReportSendTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly Dialogs _dialogs = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private async Task<ReportDialogViewModel> SavedAsync(IReportSink? sink, bool hasChannel = true, string message = "It froze.")
    {
        var dialog = new ReportDialogViewModel(
            _localizer,
            _dialogs,
            [],
            _ => new ReportSources { CharacterId = 0, CharacterName = "", App = "0.8.1", Client = "3.7.13.17", Folder = _directory.Combine("reports") },
            _ => { },
            _ => { },
            null,
            hasChannel ? _ => Task.FromResult(sink) : null);

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);
        dialog.Message = message;
        await dialog.NextCommand.ExecuteAsync(null);
        dialog.SaveCommand.Execute(null);

        return dialog;
    }

    [Fact]
    public async Task NothingIsSentUntilTheClickAndTheSendCommandWaitsForTheSavedReport()
    {
        var sink = new FakeSink(new ReportSendResult(ReportSendStatus.Sent));
        var dialog = new ReportDialogViewModel(_localizer, _dialogs, [], _ => new ReportSources { CharacterId = 0, CharacterName = "", App = "0.8.1", Client = "3.7.13.17", Folder = _directory.Combine("reports") }, _ => { }, _ => { }, null, _ => Task.FromResult<IReportSink?>(sink));

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

        Assert.False(dialog.SendCommand.CanExecute(null));

        await dialog.NextCommand.ExecuteAsync(null);
        dialog.SaveCommand.Execute(null);

        Assert.Empty(sink.Requests);
        Assert.True(dialog.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task AReportSentSaysSoWithItsCodeCannotBeSentTwiceAndCarriesTheMessageAndTheManifest()
    {
        var sink = new FakeSink(new ReportSendResult(ReportSendStatus.Sent));
        var dialog = await SavedAsync(sink);

        await dialog.SendCommand.ExecuteAsync(null);

        var request = Assert.Single(sink.Requests);

        Assert.True(dialog.IsSent);
        Assert.False(dialog.IsSending);
        Assert.Equal(1, dialog.SendProgress, 3);
        Assert.Contains(dialog.Written!.Code, dialog.SendText, StringComparison.Ordinal);
        Assert.Equal(dialog.Written.Path, request.FilePath);
        Assert.Equal("It froze.", request.Message);
        Assert.Equal(dialog.Written.Code, request.Manifest.Code);
        Assert.False(dialog.SendCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(ReportSendStatus.TooLarge, "more than the channel takes")]
    [InlineData(ReportSendStatus.RateLimited, "wait 3 seconds")]
    [InlineData(ReportSendStatus.ChannelGone, "no longer takes reports")]
    [InlineData(ReportSendStatus.Rejected, "could not be sent")]
    [InlineData(ReportSendStatus.Unavailable, "could not be sent")]
    public async Task AFailureSavesTheReportSaysWhatToDoWithItsCodeAndMayBeTriedAgain(ReportSendStatus status, string expected)
    {
        var sink = new FakeSink(new ReportSendResult(status, "Detail.", TimeSpan.FromSeconds(2.2)));
        var dialog = await SavedAsync(sink);

        await dialog.SendCommand.ExecuteAsync(null);

        Assert.False(dialog.IsSent);
        Assert.Contains(expected, dialog.SendText, StringComparison.Ordinal);

        // A rate limit is a wait, not a failure: the file is still there for the next try, and the code is on the screen.
        if (status is not ReportSendStatus.RateLimited)
            Assert.Contains(dialog.Written!.Code, dialog.SendText, StringComparison.Ordinal);

        Assert.True(File.Exists(dialog.Written!.Path));
        Assert.True(dialog.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task WithNoChannelFoundTheDialogSaysTheFileIsSavedToHandOver()
    {
        var dialog = await SavedAsync(null);

        await dialog.SendCommand.ExecuteAsync(null);

        Assert.False(dialog.IsSent);
        Assert.Contains("No channel", dialog.SendText, StringComparison.Ordinal);
        Assert.Contains(dialog.Written!.Code, dialog.SendText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAppThatSendsNothingHidesTheButton()
    {
        var dialog = await SavedAsync(null, hasChannel: false);

        Assert.False(dialog.CanSend);
        Assert.False(dialog.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClosingTheDialogWhileSendingCancelsTheSendAndKeepsTheFile()
    {
        var started = new TaskCompletionSource();
        var sink = new FakeSink(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);

            return new ReportSendResult(ReportSendStatus.Sent);
        });
        var dialog = await SavedAsync(sink);
        var sending = dialog.SendCommand.ExecuteAsync(null);

        await started.Task;

        Assert.True(dialog.IsSending);
        Assert.False(dialog.SendCommand.CanExecute(null));

        dialog.CancelCommand.Execute(null);
        await sending;

        Assert.False(dialog.IsSent);
        Assert.False(dialog.IsSending);
        Assert.Contains("cancelled", dialog.SendText, StringComparison.Ordinal);
        Assert.True(File.Exists(dialog.Written!.Path));
    }

    private sealed class FakeSink : IReportSink
    {
        private readonly Func<CancellationToken, Task<ReportSendResult>> _result;

        public FakeSink(ReportSendResult result)
        {
            _result = _ => Task.FromResult(result);
        }

        public FakeSink(Func<CancellationToken, Task<ReportSendResult>> result)
        {
            _result = result;
        }

        public List<ReportSendRequest> Requests { get; } = [];

        public long MaxFileBytes => long.MaxValue;

        public async Task<ReportSendResult> SendAsync(ReportSendRequest request, IProgress<long>? progress, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            progress?.Report(new FileInfo(request.FilePath).Length);

            return await _result(cancellationToken);
        }
    }
}
