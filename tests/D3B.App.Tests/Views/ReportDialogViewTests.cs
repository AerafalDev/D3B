using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.App.Views;
using D3B.Bot.Reports;

namespace D3B.App.Tests.Views;

public sealed class ReportDialogViewTests
{
    private static List<string> Texts(Control root)
    {
        return
        [
            .. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0),
            .. root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content).OfType<string>(),
            .. root.GetVisualDescendants().OfType<CheckBox>().Where(box => box.IsEffectivelyVisible).Select(box => box.Content).OfType<string>(),
        ];
    }

    [Fact]
    public async Task TheDialogShowsItsFormThenItsPreviewInBothLanguages()
    {
        var folder = Path.Combine(Path.GetTempPath(), "D3B.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            var result = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ReportDialogViewTests).Assembly).Dispatch(
                async () =>
                {
                    var localizer = Localizer.Instance;

                    localizer.Use(Localizer.English);

                    var character = new ReportCharacterChoice(1, 11, "Aldebarane", "Level 80, online");
                    var dialog = new ReportDialogViewModel(
                        localizer,
                        new Dialogs(),
                        [character],
                        choice => new ReportSources { CharacterId = choice?.Id ?? 0, CharacterName = choice?.Name ?? "", App = "0.8.1", Client = "3.7.13.17", Folder = folder },
                        _ => { },
                        _ => { });
                    var view = new ReportDialogView { DataContext = dialog };
                    var window = new Window { Content = view, Width = 700, Height = 900 };

                    window.Show();
                    Dispatcher.UIThread.RunJobs();

                    var form = Texts(view);

                    Drawings.Draw(window, "report-form-en");
                    localizer.Use(Localizer.French);
                    Dispatcher.UIThread.RunJobs();

                    var french = Texts(view);

                    localizer.Use(Localizer.English);
                    dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);
                    dialog.Message = "It froze.";
                    await dialog.NextCommand.ExecuteAsync(null);
                    Dispatcher.UIThread.RunJobs();

                    var preview = Texts(view);

                    Drawings.Draw(window, "report-preview-en");
                    dialog.SaveCommand.Execute(null);
                    Dispatcher.UIThread.RunJobs();

                    var done = Texts(view);

                    Drawings.Draw(window, "report-done-en");
                    window.Close();

                    return (form, french, preview, done);
                },
                TestContext.Current.CancellationToken);

            Assert.Contains("Report a bug", result.form);
            Assert.Contains("Your message (optional)", result.form);
            Assert.Contains("0 / 1,500", result.form);
            Assert.Contains("Preview", result.form);
            Assert.DoesNotContain(result.form, text => text.StartsWith('[') && text.EndsWith(']'));
            Assert.Contains("Signaler un bug", result.french);
            Assert.Contains("Ton message (facultatif)", result.french);
            Assert.DoesNotContain(result.french, text => text.StartsWith('[') && text.EndsWith(']'));
            Assert.Contains("Save the report", result.preview);
            Assert.Contains("Your message", result.preview);
            Assert.DoesNotContain("Preview", result.preview);
            Assert.Contains("Report ready", result.done);
            Assert.Contains(result.done, text => text.StartsWith("Your code is D3B-", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }
}
