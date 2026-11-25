using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Pages;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class CharacterMapViewTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CharacterMapViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static string ZoomShown(Border bar)
    {
        return bar.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text?.EndsWith('%') is true).Text!;
    }

    private static Button Named(Border bar, string name)
    {
        return bar.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == name);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheCharactersMapZoomsAroundThePointerPansOnceZoomedAndFitsAgain()
    {
        using var game = new TestGameData();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(game.Texts);
        builder.Services.AddSingleton(game.Tables);
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));

        using var host = builder.Build();
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var seen = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };
            var seen = new List<string>();

            main.Refresh();
            window.Show();
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var tab = (CharacterTabViewModel)main.Editor.Selected!.Content;
            var canvas = window.GetVisualDescendants().OfType<MapCanvas>().Single(map => map.Name == "MapCanvas");
            var bar = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "MapZoomBar");
            var pointer = canvas.TranslatePoint(new Point(canvas.Bounds.Width * 0.3, canvas.Bounds.Height * 0.6), window)!.Value;
            var middle = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2), window)!.Value;

            window.MouseMove(pointer);

            var under = tab.Map.HoveredCell;

            seen.Add($"{ZoomShown(bar)} on a cell:{under is not null} out:{Named(bar, "Zoom out").IsEffectivelyEnabled} fit:{Named(bar, "See the whole map").IsEffectivelyEnabled}");
            Drawings.Draw(window, "character-map-fit");

            // Two notches of the wheel zoom in, the cell under the pointer still under it.
            window.MouseWheel(pointer, new Vector(0, 2));
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(pointer);
            seen.Add($"{ZoomShown(bar)} same cell:{tab.Map.HoveredCell == under} out:{Named(bar, "Zoom out").IsEffectivelyEnabled}");
            Drawings.Draw(window, "character-map-zoomed");

            // A drag moves the zoomed map: another cell comes under the pointer.
            window.MouseDown(pointer, MouseButton.Left);
            window.MouseMove(new Point(pointer.X - 120, pointer.Y - 60));
            window.MouseUp(new Point(pointer.X - 120, pointer.Y - 60), MouseButton.Left);
            window.MouseMove(pointer);
            seen.Add($"moved:{tab.Map.HoveredCell != under}");

            // The bar zooms around the map's middle: the middle's cell stays, one off it changes.
            window.MouseMove(middle);

            var centred = tab.Map.HoveredCell;

            window.MouseMove(pointer);

            var offCentre = tab.Map.HoveredCell;

            Named(bar, "Zoom in").Command!.Execute(null);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(pointer);

            var zoomed = tab.Map.HoveredCell != offCentre;

            window.MouseMove(middle);
            seen.Add($"{ZoomShown(bar)} same middle:{tab.Map.HoveredCell == centred} zoomed:{zoomed}");

            Named(bar, "See the whole map").Command!.Execute(null);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(pointer);
            seen.Add($"{ZoomShown(bar)} same cell:{tab.Map.HoveredCell == under} fit:{Named(bar, "See the whole map").IsEffectivelyEnabled}");

            // A tab opened again on a zoomed map shows its middle, the zoom applied once the canvas has its size.
            window.MouseMove(middle);

            var fittedMiddle = tab.Map.HoveredCell;

            Named(bar, "Zoom in").Command!.Execute(null);
            Named(bar, "Zoom in").Command!.Execute(null);
            main.Editor.Close(main.Editor.Selected);
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            bar = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "MapZoomBar");
            window.MouseMove(middle);
            seen.Add($"{ZoomShown(bar)} same middle:{tab.Map.HoveredCell == fittedMiddle} same tab:{ReferenceEquals(main.Editor.Selected!.Content, tab)}");

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            seen.Add($"{ZoomShown(bar)} {AutomationProperties.GetName(bar.GetVisualDescendants().OfType<Button>().First())}");

            localizer.Use(Localizer.English);
            window.Close();

            return seen;
        });

        Assert.Equal(
            [
                "100 % on a cell:True out:False fit:False",
                "149 % same cell:True out:True",
                "moved:True",
                "186 % same middle:True zoomed:True",
                "100 % same cell:True fit:False",
                "156 % same middle:True same tab:True",
                "156 % Dézoomer",
            ],
            seen);
    }
}
