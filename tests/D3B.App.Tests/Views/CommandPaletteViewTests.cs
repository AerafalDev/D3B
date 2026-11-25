using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class CommandPaletteViewTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandPaletteViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task CtrlShiftPShowsThePaletteItsSignTypedTheCommandsAndACharactersPlayByItsName()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (centre, query, texts) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();

            var centre = window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("TitleSearch"));
            var shown = centre.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

            window.KeyPressQwerty(PhysicalKey.P, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            var palette = window.GetVisualDescendants().OfType<SearchView>().Single();

            Drawings.Draw(window, "studio-palette");

            var field = palette.GetVisualDescendants().OfType<TextBox>().Single();
            var query = field.Text;

            // The palette shows its first thirty: a character's commands come by its words.
            window.KeyTextInput("jouer");
            Dispatcher.UIThread.RunJobs();

            // A result's name is in runs, the text found lit.
            var texts = palette.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Inlines is { Count: > 0 } runs ? string.Concat(runs.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text)) : block.Text).OfType<string>().ToList();

            window.Close();

            return (shown, query, texts);
        });

        // next's command centre: the app's name, not a field's word (D540).
        Assert.Equal(["D3B"], centre);
        Assert.Equal(">", query);
        Assert.Contains("Jouer Character", texts);
        Assert.DoesNotContain("Afficher toutes les commandes", texts);
    }
}
