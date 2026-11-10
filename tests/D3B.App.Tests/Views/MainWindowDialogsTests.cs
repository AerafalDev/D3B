using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.App.Views;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class MainWindowDialogsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static List<string> Texts(Control root)
    {
        return
        [
            .. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0),
            .. root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content).OfType<string>(),
        ];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowDialogsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task HelpReportABugOpensTheReportDialogInTheWindow()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var shown = await OnTheUIThreadAsync(() =>
        {
            Localizer.Instance.Use(Localizer.English);

            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var item = window.GetVisualDescendants().OfType<Menu>().Single().GetLogicalDescendants().OfType<MenuItem>().Single(menu => ReferenceEquals(menu.Command, main.ShowReportCommand));

            item.Command!.Execute(item.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            var views = window.GetVisualDescendants().OfType<ReportDialogView>().Where(view => view.IsEffectivelyVisible).ToList();

            window.Close();

            return views.Count;
        });

        Assert.Equal(1, shown);
    }

    [Fact]
    public void EveryDialogViewModelOfTheAppHasARowInTheWindowsTable()
    {
        var dialogs = typeof(MainWindow).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDialogViewModel).IsAssignableFrom(type)).ToList();

        Assert.NotEmpty(dialogs);
        Assert.Empty(dialogs.Except(MainWindow.DialogViewModels));
        Assert.Empty(MainWindow.DialogViewModels.Except(dialogs));
    }

    [Fact]
    public async Task AQuestionIsAskedInTheWindow()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var asked = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            main.Dialogs.Ask(new QuestionViewModel(main.Dialogs, "Quit now?", null, "Quit", "Stay", () => { }));
            Dispatcher.UIThread.RunJobs();

            var asked = window.GetVisualDescendants().OfType<QuestionView>().Count(view => view.IsEffectivelyVisible);

            window.Close();

            return asked;
        });

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task TheReportRunsInTheWindowFromItsFormToItsPreviewAtAWidthThatHoldsIt()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var (form, preview, width) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowDialogsTests).Assembly).Dispatch(
            async () =>
            {
                Localizer.Instance.Use(Localizer.English);

                var main = new MainViewModel(runner, Localizer.Instance);
                var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

                main.Refresh();
                window.Show();
                main.ShowReportCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                var dialog = Assert.IsType<ReportDialogViewModel>(main.Dialogs.Current);
                var view = window.GetVisualDescendants().OfType<ReportDialogView>().Single();
                var form = Texts(view);

                Drawings.Draw(window, "report-window-form-en");
                dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);
                dialog.Message = "It froze.";
                await dialog.NextCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();

                var preview = Texts(view);

                Drawings.Draw(window, "report-window-preview-en");
                window.Close();

                return (form, preview, view.Bounds.Width);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Report a bug", form);
        Assert.Contains("Your message (optional)", form);
        Assert.Contains("Save the report", preview);
        Assert.Contains("Your message", preview);
        Assert.InRange(width, 520, 640);
    }
}
