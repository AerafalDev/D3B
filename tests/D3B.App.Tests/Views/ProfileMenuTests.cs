using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class ProfileMenuTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ProfileMenuTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheProfileFieldIsASelectWhoseMenuOffersTheProfilesThenNone()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);
        var (_, bot) = await _play.EnterAsync(300);

        var (flyouts, headers) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            main.OpenCharacter(bot.Key);
            main.Selection.SelectCharacter(bot.Key);
            window.Show();
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            Slides.Finish();

            // The Details' field and the tab's: each the button's own flyout, which a click opens and the next one closes (D548).
            var fields = window.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "ProfileField").ToList();
            var items = new List<object>();

            ProfileMenu.Fill(items, main.Properties.Character);

            window.Close();

            return (fields.Select(field => field.Flyout is MenuFlyout).ToList(), items.OfType<MenuItem>().Select(item => item.Header as string).ToList());
        });

        Assert.Equal([true, true], flyouts);
        Assert.Equal("No profile", headers[^1]);
        Assert.Contains("Patrol", headers);
    }
}
