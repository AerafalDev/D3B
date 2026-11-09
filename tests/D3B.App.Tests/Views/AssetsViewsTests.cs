using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Assets;
using D3B.App.Views;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.Views;

public sealed class AssetsViewsTests
{
    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? [])).Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AssetsViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheAssetsTreeAndAProfilesTabShowInBothLanguages()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack, Walks = 42 }), ct);

        await store.Assets.SaveProfileAsync("Ash", TestStore.HarvestId, "{}", ct);
        await store.Assets.CreateFolderAsync("Lumberjack", ct);
        await store.Assets.MoveAsync(walker.Id, "Lumberjack", ct);
        await File.WriteAllTextAsync(Path.Combine(store.Assets.Folder, "Broken.profile.json"), "{", ct);
        await store.Assets.LoadAsync([.. store.Assets.Assets.Where(asset => asset.IsGiven)], ct);
        await store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));
        await store.Accounts.AssignAsync(TestPlay.Key, walker.Id, ct);

        var runner = await TestHosts.RunningAsync(store.Host);
        var (english, french, tab, add) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var dock = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "AssetsDock");
            var english = Texts(dock);
            var panel = dock.GetVisualDescendants().OfType<AssetsView>().Single();

            var menu = panel.AddMenu();
            var add = (menu is not null, menu?.Items.OfType<MenuItem>().Select(item => (item.Header as string ?? (item.Header as Control)?.GetVisualDescendants().OfType<TextBlock>().LastOrDefault()?.Text ?? string.Concat((item.Header as DockPanel)?.Children.OfType<TextBlock>().Select(block => block.Text) ?? []), item.IsEnabled)).ToList());

            main.OpenAsset(walker.Id);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var view = window.GetVisualDescendants().OfType<ProfileView>().Single(view => view.IsEffectivelyVisible);

            english.AddRange(Texts(view));
            Drawings.Draw(window, "assets-en");
            main.Settings.UseFrenchCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var french = Texts(dock).Concat(Texts(view)).ToList();

            Drawings.Draw(window, "assets-fr");
            Drawings.DrawPart(dock, "part-assets-fr");
            Drawings.DrawPart(view, "part-profile-fr");

            var tab = (main.Editor.Selected?.Key, main.Editor.Selected?.Title, main.Properties.ShowsAsset);

            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, tab, add);
        });

        Assert.Contains("Lumberjack", english);
        Assert.Contains("Walker", english);
        Assert.Contains("Broken.profile.json", english);
        Assert.Contains("Given with D3B", english);
        Assert.Contains("Variant of", english);
        Assert.Contains("Preview", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Donnés avec D3B", french);
        Assert.Contains("Variante de", french);
        Assert.Contains("Tout remettre", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Equal(("profile:" + walker.Id, "Walker", true), tab);

        // Add offers each kind, the profile alone made today, then a folder and an import.
        Assert.True(add.Item1);
        Assert.Contains(("Profile", true), add.Item2!);
        Assert.Contains(("Folder", true), add.Item2!);
        Assert.Contains(add.Item2!, item => item is { Item2: false });
        Assert.All(english.Concat(french), text => Assert.DoesNotContain("Avalonia.", text, StringComparison.Ordinal));
        Assert.All(english.Concat(french), text => Assert.DoesNotContain("D3B.App.", text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADoubleClickOnAnAssetOpensItAlthoughTheFirstClickBuildsTheRowsAgain()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var ash = await store.Assets.SaveProfileAsync("Ash", TestStore.HarvestId, "{}", ct);
        var runner = await TestHosts.RunningAsync(store.Host);
        var tab = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var row = window.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("AssetRow") && border.DataContext is AssetRowViewModel { Name: "Ash" });
            var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var chosen = main.Editor.Selected?.Key;

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var opened = main.Editor.Selected?.Key;

            window.Close();

            return (chosen, opened);
        });

        Assert.NotEqual("profile:" + ash.Id, tab.chosen);
        Assert.Equal("profile:" + ash.Id, tab.opened);
    }
}
