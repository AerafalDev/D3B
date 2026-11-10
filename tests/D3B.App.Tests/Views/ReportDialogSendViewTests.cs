using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.App.Views;
using D3B.Bot.Reports;
using D3B.Data.Tests;

namespace D3B.App.Tests.Views;

public sealed class ReportDialogSendViewTests
{
    [Fact]
    public async Task OnceTheReportIsSentTheSendButtonAndTheProgressAreGone()
    {
        using var directory = new TemporaryDirectory();
        var release = new TaskCompletionSource();

        var (during, after) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ReportDialogSendViewTests).Assembly).Dispatch(
            async () =>
            {
                Localizer.Instance.Use(Localizer.English);

                var sink = new WaitingSink(release.Task);
                var dialog = new ReportDialogViewModel(Localizer.Instance, new Dialogs(), [], _ => new ReportSources { CharacterId = 0, CharacterName = "", App = "0.9.0", Client = "3.7.13.17", Folder = directory.Combine("reports") }, _ => { }, _ => { }, null, _ => Task.FromResult<IReportSink?>(sink));
                var view = new ReportDialogView { DataContext = dialog };
                var window = new Window { Content = view, Width = 700, Height = 500 };

                window.Show();
                dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);
                await dialog.NextCommand.ExecuteAsync(null);

                var sending = dialog.SaveAndSendCommand.ExecuteAsync(null);

                Dispatcher.UIThread.RunJobs();

                var during = (Buttons(view), view.GetVisualDescendants().OfType<ProgressBar>().Count(bar => bar.IsEffectivelyVisible));

                Drawings.Draw(window, "report-sending-en");
                release.SetResult();
                await sending;
                Dispatcher.UIThread.RunJobs();

                var after = (Buttons(view), view.GetVisualDescendants().OfType<ProgressBar>().Count(bar => bar.IsEffectivelyVisible));

                Drawings.Draw(window, "report-sent-en");
                window.Close();

                return (during, after);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Send to the maintainers", during.Item1);
        Assert.Equal(1, during.Item2);
        Assert.DoesNotContain("Send to the maintainers", after.Item1);
        Assert.Contains("Close", after.Item1);
        Assert.DoesNotContain("Copy the code", after.Item1);
        Assert.Equal(0, after.Item2);
    }

    private static List<string> Buttons(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content).OfType<string>()];
    }

    private sealed class WaitingSink : IReportSink
    {
        private readonly Task _release;

        public WaitingSink(Task release)
        {
            _release = release;
        }

        public long MaxFileBytes => long.MaxValue;

        public async Task<ReportSendResult> SendAsync(ReportSendRequest request, IProgress<long>? progress, CancellationToken cancellationToken)
        {
            await _release.ConfigureAwait(false);

            return new ReportSendResult(ReportSendStatus.Sent);
        }
    }
}
