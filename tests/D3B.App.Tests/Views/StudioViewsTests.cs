using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class StudioViewsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StudioViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheRunBlockShowsOnlyTheButtonsTheChosenCharactersStateAllows()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (none, stopped, playing, paused) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            List<string> Buttons()
            {
                main.Refresh();
                Dispatcher.UIThread.RunJobs();

                var run = window.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("Run"));

                return [.. run.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("Run") && button.IsVisible).Select(button => button.IsEnabled ? "on" : "off")];
            }

            main.Refresh();
            window.Show();
            Drawings.Draw(window, "studio-welcome");

            var none = (Buttons(), main.RunBlock.Name);

            Drawings.DrawPart(window.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("Run")), "part-run-none");
            Drawings.DrawPart(window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("TitleSearch")), "part-search");

            main.Bots.Items[0].ChooseCommand.Execute(null);

            var stopped = (Buttons(), main.RunBlock.State, main.Bots.Items[0].IsChosen);

            main.RunBlock.PlayCommand.Execute(null);

            var playing = (Buttons(), main.RunBlock.State);

            Drawings.Draw(window, "studio-playing");
            Drawings.DrawPart(window.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("Run")), "part-run-playing");
            Drawings.DrawPart(window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "OutlinerDock"), "part-characters");
            Drawings.DrawPart(window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "BottomDock"), "part-bottom");
            main.RunBlock.PauseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            localizer.Use(Localizer.English);
            window.Close();

            return (none, stopped, playing, main.RunBlock.State);
        });

        Assert.Equal(["off"], none.Item1);
        Assert.Equal("Aucun personnage", none.Name);
        Assert.Equal(["on"], stopped.Item1);
        Assert.Equal((RunState.Stopped, true), (stopped.State, stopped.Item3));
        Assert.Equal((2, RunState.Playing), (playing.Item1.Count, playing.State));
        Assert.Equal(RunState.Playing, paused);

        // The pause goes through the bot's loop: once there, play and stop show.
        var bot = Assert.Single(_play.Bots.All);

        await Waiting.UntilAsync(() => bot.IsPaused, TimeSpan.FromSeconds(10));

        var resumed = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);

            main.Refresh();
            main.RunBlock.Choose(main.Bots.Items[0]);

            return (main.RunBlock.State, main.RunBlock.ShowsPlay, main.RunBlock.ShowsStop, main.RunBlock.ShowsPause);
        });

        Assert.Equal((RunState.Paused, true, true, false), resumed);
    }

    [Fact]
    public async Task TheSearchListsWhatItFindsGroupedAndAResultOpensIt()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (everywhere, commands, opened) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            main.Search.OpenCommand.Execute(SearchScope.All);
            main.Search.Query = "char";
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-search");

            var everywhere = Texts(window.GetVisualDescendants().OfType<SearchView>().Single());

            main.Search.Query = ">param";
            Dispatcher.UIThread.RunJobs();

            var commands = (main.Search.ShownScope, main.Search.Groups.SelectMany(group => group.Results).Select(result => result.Item.Title).ToList());

            main.Search.RunActive();
            Dispatcher.UIThread.RunJobs();
            // The language given back first: changed once the window is gone, its texts would be laid out with no app.
            localizer.Use(Localizer.English);
            window.Close();

            return (everywhere, commands, (main.Search.IsOpen, main.Editor.Selected?.Key));
        });

        Assert.Contains("Personnages", everywhere);
        Assert.Contains("Niveau 200", everywhere);
        Assert.Contains("Commandes", everywhere);
        Assert.Equal(SearchScope.Commands, commands.ShownScope);
        Assert.Equal(["Paramètres"], commands.Item2);
        Assert.Equal((false, "settings"), opened);
    }

    [Fact]
    public async Task TheStudiosPanelsCloseAndTheEditorTakesTheirRoom()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var widths = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var editor = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "EditorDock");
            var before = (editor.Bounds.Width, editor.Bounds.Height);

            main.Layout.Close(StudioPanel.Outliner);
            main.Layout.Close(StudioPanel.Assets);
            main.Layout.Close(StudioPanel.Bottom);
            main.Layout.Close(StudioPanel.Details);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "studio-closed");

            var after = (editor.Bounds.Width, editor.Bounds.Height);

            main.Layout.Reset();
            Dispatcher.UIThread.RunJobs();
            window.Close();

            return (before, after);
        });

        // 1440 less the window's edges, the left column, the inspector and their gaps, then all of it.
        Assert.Equal(1440 - 2 - 10 - 300 - 340 - 10, widths.before.Width, 1);
        Assert.Equal(1440 - 2 - 10, widths.after.Width, 1);
        Assert.True(widths.after.Height > widths.before.Height + 200);
    }

    [Fact]
    public void TheSearchSitsInTheMiddleOfTheRowUntilASideWouldTouchIt()
    {
        Assert.Equal((550d, 340d), TitleBarPanel.Middle(1440, 300, 450, 340, 200, 12));
        Assert.Equal((312d, 236d), TitleBarPanel.Middle(1000, 300, 440, 340, 200, 12));
        Assert.Equal(0, TitleBarPanel.Middle(800, 300, 440, 340, 200, 12).Width);
    }
}
