using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Assets;
using D3B.Bot.Routes;

namespace D3B.App.Tests.Views;

public sealed class SequenceViewsTests
{
    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? [])).Where(text => text.Length > 0)];
    }

    // The language is the window's one, shared by every test of the session: it is set, and read, on the interface's thread alone.
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SequenceViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AListOfStepsIsEditedInATabAndAProfileThatPlaysItIsMadeFromTheNewProfileDialog()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var south = await store.Assets.SaveAsync(new RouteAsset { Id = "r-south", Name = "South", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, TestContext.Current.CancellationToken);
        var runner = await TestHosts.RunningAsync(store.Host);
        var result = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var editor = (await main.Sequences.CreateAsync(""))!;

            editor.AddStepCommand.Execute(RouteActions.Follow);
            editor.AddStepCommand.Execute("wait");
            Dispatcher.UIThread.RunJobs();

            var list = window.GetVisualDescendants().OfType<SequenceView>().Single(view => view.IsEffectivelyVisible);
            var before = Texts(list);
            var route = Assert.IsType<RouteFieldViewModel>(editor.Steps[0].Editor!.FieldOf("Route"));

            route.PickCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = Assert.IsType<RoutePickerViewModel>(main.Dialogs.Current);
            var offered = picker.Routes.Select(choice => (choice.Name, choice.Detail)).ToList();

            Drawings.Draw(window, "sequence-route-picker");
            picker.Routes.Single().ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "sequence-editor");

            var after = Texts(list);

            await editor.SaveCommand.ExecuteAsync(null);
            main.Profiles.NewCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var dialog = Assert.IsType<NewProfileViewModel>(main.Dialogs.Current);

            dialog.PlaysBehavior = true;
            dialog.Behavior = dialog.Behaviors.Single(choice => choice.Id == editor.Id);
            dialog.Name = "South run";
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "sequence-new-profile");

            dialog.CreateCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var profile = Assert.IsType<ProfileViewModel>(main.Profiles.Editing);
            var tab = window.GetVisualDescendants().OfType<ProfileView>().Single(view => view.IsEffectivelyVisible);
            var profileTexts = Texts(tab);

            Drawings.Draw(window, "sequence-profile");
            await profile.SaveCommand.ExecuteAsync(null);
            profile.EditStepsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var reopened = main.Editor.Selected?.Content;

            window.Close();

            return (before, offered, after, profileTexts, reopened, Saved: main.Profiles.Items.Count(item => item.IsBehaviorProfile), main.IsSaved);
        });

        Assert.Contains("Follow the route choose a route", string.Concat(result.before));
        Assert.Contains("Wait 5 s", string.Concat(result.before));
        Assert.Equal([("South", "2 maps")], result.offered);
        Assert.Contains("Follow the route South", string.Concat(result.after));
        Assert.Contains("Edit the steps", string.Concat(result.profileTexts));
        Assert.Contains("2 steps", string.Concat(result.profileTexts));
        Assert.Equal(1, result.Saved);
        Assert.IsType<D3B.App.ViewModels.Sequences.SequenceEditorViewModel>(result.reopened);
        Assert.Equal([south.Id], store.Assets.OfType<BehaviorAsset>().SelectMany(behavior => behavior.Uses()));
    }
}
