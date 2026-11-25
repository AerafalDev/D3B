using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;

namespace D3B.App.Tests.Views;

public sealed class EmptyStatesViewTests : IAsyncDisposable
{
    private readonly TestStore _store = new();

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(EmptyStatesViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    [Fact]
    public async Task TheEmptyCharactersAndWorldViewsGuideAndTheWorldsOffersARouteAndAZone()
    {
        await _store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(_store.Host);
        var (characters, world) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            StackPanel Empty()
            {
                return window.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "EmptyState" && panel.IsEffectivelyVisible);
            }

            Drawings.Draw(window, "studio-empty-characters");

            var characters = Texts(Empty());

            main.Layout.ShowView(SideView.World);
            Dispatcher.UIThread.RunJobs();

            var world = (Texts(Empty()), Empty().GetVisualDescendants().OfType<Button>().Count(button => button.IsEffectivelyVisible));

            window.Close();

            return (characters, world);
        });

        Assert.Equal(["Aucun personnage connecté", "D3B relaie le jeu : un client lancé à travers lui montre ici ses personnages.", "1. Gardez D3B ouvert : sa barre d'état dit « Relais prêt ».", "2. Lancez Dofus depuis le launcher d'Ankama.", "3. Choisissez un personnage : il s'affiche ici."], characters);
        Assert.Equal("Aucune route ni zone", world.Item1[0]);
        Assert.Equal(2, world.Item2);
    }
}
