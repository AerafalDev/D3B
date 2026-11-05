using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Routes;
using D3B.Data.Tests;
using D3B.Data.Maps;
using D3B.Data.Packs;

namespace D3B.App.Tests.Views;

public sealed class RouteActionsViewTests
{
    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? [])).Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RouteActionsViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    // Maps 1 to 5 of the route world, each of walkable cells, those on the edge leading off, and a door's element on cell 300.
    private static GamePack PackOfMaps(TemporaryDirectory directory)
    {
        var cells = new MapCell[MapData.CellCount];

        for (var cell = 0; cell < cells.Length; cell++)
        {
            var edge = cell < 14 || cell >= MapData.CellCount - 14 || cell % 14 is 0 or 13;

            cells[cell] = new MapCell(MapCellProperties.Walkable, 0, 0, edge ? 1 : 0, 0, 0x10, 0);
        }

        var info = new PackInfo { ClientVersion = "3.7.13.17", Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };
        var path = directory.Combine("data-10.pack");

        using (var stream = File.Create(path))
        using (var writer = new PackWriter(stream, info, 10))
        {
            for (var id = 1L; id <= 5; id++)
                writer.Add(MapData.EntryName(id), new MapData(id, 0, 0, 0, 0, cells, new[] { new MapInteractive(1, 300, 77) }));

            writer.Complete();
        }

        return GamePack.Open(path);
    }

    [Fact]
    public async Task TheActionsOfAStepAreSentencesOpenedToTheirValuesWithTheCellPickedOnTheMapsOwnCells()
    {
        await using var store = new TestStore();
        using var directory = new TemporaryDirectory();
        using var pack = PackOfMaps(directory);

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var localizer = Localizer.Instance;

        localizer.Use(Localizer.English);

        var main = new MainViewModel(runner, localizer);

        main.Refresh();
        main.Routes.Use(TestRouteWorld.Services(store.Assets) with { Maps = new GameMaps(pack) });

        var editor = (await main.Routes.CreateAsync(""))!;

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: false);
        editor.Select(0);
        editor.OfferedActions.Single(action => action.Node == RouteActions.Exit).AddCommand.Execute(null);
        editor.OfferedActions.Single(action => action.Node == RouteActions.Wait).AddCommand.Execute(null);

        var result = await OnTheUIThreadAsync(() =>
        {
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var properties = window.GetVisualDescendants().OfType<PropertiesView>().Single(view => view.IsEffectivelyVisible);
            var texts = Texts(properties);
            var exit = editor.Actions[0];
            var cell = exit.Editor!.Sections.SelectMany(section => section.Fields).OfType<CellFieldViewModel>().Single();

            Assert.True(cell.CanPick);
            cell.PickCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = Assert.IsType<CellPickerViewModel>(main.Dialogs.Current);
            var picked = window.GetVisualDescendants().OfType<CellPicker>().Single();

            Drawings.Draw(window, "route-actions-cell-picker");
            picker.Choose(300);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "route-actions");

            var after = Texts(properties);

            window.Close();

            return (texts, after, picked.Map?.Id, main.Dialogs.IsOpen, editor.Steps[0].ActionsText, editor.IsDirty);
        });

        Assert.Contains("Actions on step 1", result.texts);
        Assert.Contains("Leave by the top, or by cell none", result.texts);
        Assert.Contains("Wait 5 s", result.texts);
        Assert.Contains("2 actions", result.texts);
        Assert.Contains("Leave by the top, or by cell 300", result.after);
        Assert.Equal(1L, result.Id);
        Assert.False(result.IsOpen);
        Assert.Equal("2 actions", result.ActionsText);
        Assert.True(result.IsDirty);
    }
}
