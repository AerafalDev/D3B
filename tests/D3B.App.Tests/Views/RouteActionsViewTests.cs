using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Npcs;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Studio.Routes;
using Microsoft.Extensions.DependencyInjection;

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

    // The language is the window's one, shared by every test of the session: it is set, and read, on the interface's thread alone.
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
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
        var result = await OnTheUIThreadAsync(async () =>
        {
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
            editor.Steps[0].AddActionCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "route-action-picker");
            (main.Dialogs.Current as RouteActionPickerViewModel)?.CancelCommand.Execute(null);

            var after = Texts(properties);

            window.Close();
            localizer.Use(Localizer.English);

            return (texts, after, picked.Map?.Id, main.Dialogs.IsOpen, editor.Steps[0].ActionsText, editor.IsDirty);
        });

        Assert.Contains("Actions on step 1", result.texts);
        Assert.Contains("Leave by ", result.texts);
        Assert.Contains("the top", result.texts);
        Assert.Contains("choose", result.texts);
        Assert.Contains("Wait ", result.texts);
        Assert.Contains("2 actions", result.texts);
        Assert.DoesNotContain("choose", result.after);
        Assert.Contains("300", result.after);
        Assert.Equal(1L, result.Id);
        Assert.False(result.IsOpen);
        Assert.Equal("2 actions", result.ActionsText);
        Assert.True(result.IsDirty);
    }

    [Fact]
    public async Task AValueOfAnActionAloneShowsAsItsFieldWhereTheWholeEditorShowsItsSections()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var shown = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets));

            var editor = (await main.Routes.CreateAsync(""))!;

            editor.TraceSquare(1, 0, isDrag: false);
            editor.Select(0);
            editor.OfferedActions.Single(action => action.Node == RouteActions.Wait).AddCommand.Execute(null);

            var field = editor.Actions[0].Parts.Single(part => part.IsToken).Field!;
            var alone = new SettingsFormView { DataContext = field };
            var whole = new SettingsFormView { DataContext = editor.Actions[0].Editor };
            var window = new Window { Content = new StackPanel { Children = { alone, whole } }, Width = 500, Height = 400 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var result = (alone.GetVisualDescendants().OfType<NumericUpDown>().Count(), whole.GetVisualDescendants().OfType<NumericUpDown>().Count(), Texts(alone), Texts(whole));

            window.Close();

            return result;
        });

        Assert.Equal(1, shown.Item1);
        Assert.Equal(1, shown.Item2);
        Assert.DoesNotContain("Seconds", shown.Item3);
        Assert.Contains("Seconds", shown.Item4);
    }

    // What the bots learned on map 1 of the portal's dialog, as Incarnam's (D253): talking asks a question, whose reply asks another,
    // whose first reply moves the character and whose second ends the dialog.
    private static KnownNpcs LearnedNpcs()
    {
        var at = DateTimeOffset.UnixEpoch;
        var known = new KnownNpcs();

        known.Saw(1, -20_000, TestGameData.NpcId, 302, 3, at);
        known.Asked(1, TestGameData.NpcId, 30_637, [36_979], true, at);
        known.Asked(1, TestGameData.NpcId, 30_638, [36_977, 36_978], false, at);
        known.Answered(1, TestGameData.NpcId, KnownNpcs.Talk, KnownNpcs.Talk, NpcOutcomeKind.Question, 30_637, at);
        known.Answered(1, TestGameData.NpcId, 30_637, 36_979, NpcOutcomeKind.Question, 30_638, at);
        known.Answered(1, TestGameData.NpcId, 30_638, 36_977, NpcOutcomeKind.MapChange, 2, at);
        known.Answered(1, TestGameData.NpcId, 30_638, 36_978, NpcOutcomeKind.End, 0, at);

        return known;
    }

    [Fact]
    public async Task AnNpcAndTheRepliesGivenToItArePickedAmongWhatTheBotsLearnedNeverTyped()
    {
        await using var store = new TestStore();
        using var directory = new TemporaryDirectory();
        using var pack = PackOfMaps(directory);
        using var data = new TestGameData();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var provider = new ServiceCollection().AddSingleton(LearnedNpcs()).AddSingleton(data.Tables).BuildServiceProvider();
        var result = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets) with { Maps = new GameMaps(pack), Provider = provider, Texts = data.Texts });

            var editor = (await main.Routes.CreateAsync(""))!;

            editor.TraceSquare(1, 0, isDrag: false);
            editor.Select(0);
            editor.OfferedActions.Single(action => action.Node == RouteActions.Npc).AddCommand.Execute(null);

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var fields = editor.Actions[0].Editor!.Sections.SelectMany(section => section.Fields).ToList();
            var npc = fields.OfType<NpcFieldViewModel>().Single();
            var replies = fields.OfType<NpcRepliesFieldViewModel>().Single();
            var before = (npc.CanPick, replies.CanPick, editor.Actions[0].Sentence);

            npc.PickCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var npcPicker = Assert.IsType<NpcPickerViewModel>(main.Dialogs.Current);
            var offered = npcPicker.Npcs.Select(choice => (choice.Name, choice.Detail)).ToList();

            Drawings.Draw(window, "route-actions-npc-picker");
            npcPicker.Npcs.Single().ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var afterNpc = (npc.Value, replies.CanPick, editor.Actions[0].Sentence, main.Dialogs.IsOpen);

            replies.PickCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = Assert.IsType<RepliesPickerViewModel>(main.Dialogs.Current);
            var asked = picker.Question;
            var first = picker.Choices.Select(choice => (choice.Reply, choice.Name, choice.Outcome)).ToList();

            Drawings.Draw(window, "route-actions-replies-picker");
            picker.Choices.Single().ChooseCommand.Execute(null);

            var second = picker.Choices.Select(choice => (choice.Reply, choice.Name, choice.Outcome)).ToList();

            picker.Choices.Single(choice => choice.Reply == 36_977).ChooseCommand.Execute(null);

            var ended = (picker.Status, picker.HasChoices, picker.Given.ToList());

            Drawings.Draw(window, "route-actions-replies-picked");
            picker.DoneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picked = (replies.Value.ToList(), editor.Actions[0].Sentence, editor.IsDirty);

            npc.ClearCommand.Execute(null);

            var cleared = (npc.Value, replies.Value.ToList(), editor.Actions[0].Sentence);

            window.Close();
            localizer.Use(Localizer.English);

            return (before, offered, afterNpc, asked, first, second, ended, picked, cleared, picker.Title);
        });

        Assert.Equal((true, false, "Talk to NPC choose, replying none"), result.before);
        Assert.Equal([("Master Anemo", "Cell 302 · seen once · 2 questions known")], result.offered);
        Assert.Equal((1088, true, "Talk to NPC Master Anemo, replying none", false), result.afterNpc);
        Assert.Equal("Replies given to Master Anemo", result.Title);
        Assert.Equal("Welcome! Do you want to leave Incarnam?", result.asked);
        Assert.Equal([(36979, "I want to leave for Astrub", "leads to “Are you sure you want to leave?”")], result.first);
        Assert.Equal([(36977, "Yes, let's go", "moves the character to map 2"), (36978, "No, I stay", "ends the dialog")], result.second);
        Assert.False(result.ended.HasChoices);
        Assert.Equal("Nothing more is known after the last reply.", result.ended.Status);
        Assert.Equal(["1. I want to leave for Astrub: leads to “Are you sure you want to leave?”", "2. Yes, let's go: moves the character to map 2"], result.ended.Item3);
        Assert.Equal([36979, 36977], result.picked.Item1);
        Assert.Equal("Talk to NPC Master Anemo, replying I want to leave for Astrub, Yes, let's go", result.picked.Sentence);
        Assert.True(result.picked.IsDirty);
        Assert.Null(result.cleared.Value);
        Assert.Empty(result.cleared.Item2);
        Assert.Equal("Talk to NPC choose, replying none", result.cleared.Item3);
    }
}
