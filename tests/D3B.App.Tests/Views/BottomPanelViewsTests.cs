using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Behaviors;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Storage;
using D3B.Storage.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.Views;

public sealed class BottomPanelViewsTests : IAsyncDisposable
{
    private readonly TestStore _store = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(BottomPanelViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task EachTabOfTheBottomPanelShowsItsToolbarAndItsLinesInFrench()
    {
        var ct = TestContext.Current.CancellationToken;

        // Past midnight, so that the run of the last hour is today's.
        _store.Play.Time.Advance(TimeSpan.FromHours(2));

        var now = _store.Play.Time.GetUtcNow();

        await _store.OpenedAsync();
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200), (TestPlay.CharacterId + 1, "Mule", 12));
        await _store.Accounts.AssignAsync(new CharacterKey(TestPlay.AccountId, TestPlay.CharacterId + 1), "gone", ct);
        await File.WriteAllTextAsync(Path.Combine(Directory.CreateDirectory(_store.Assets.Folder).FullName, "Frigost.profile.json"), "{", ct);
        await _store.Assets.LoadAsync([.. _store.Assets.Assets.Where(asset => asset.IsGiven)], ct);

        var contexts = _store.Host.Services.GetRequiredService<IDbContextFactory<StorageContext>>();

        await using (var context = await contexts.CreateDbContextAsync(ct))
        {
            context.Runs.Add(new RunRecord
            {
                AccountId = TestPlay.AccountId,
                CharacterId = TestPlay.CharacterId,
                CharacterName = "Character",
                Title = "Ortie",
                Activity = "Harvesting",
                StartedAt = now.AddMinutes(-50),
                LastAt = now.AddMinutes(-10),
                EndedAt = now.AddMinutes(-10),
                Outcome = ActivityOutcome.Stopped,
                Items = new() { [TestPlay.Ash] = 420, [TestPlay.Chestnut] = 106 },
            });
            await context.SaveChangesAsync(ct);
        }

        await _store.Host.Services.GetRequiredService<RunBook>().LoadAsync(ct);

        var (_, bot) = await _store.Play.EnterAsync(300);

        bot.Journal.Add(new WalkEntry(now, 0, TestPlay.MapId, 388, WalkOutcome.Arrived, TimeSpan.FromMilliseconds(900)));
        bot.Journal.Add(new WalkEntry(now, 0, TestPlay.MapId, 12, WalkOutcome.Refused, TimeSpan.FromMilliseconds(100)));
        bot.Trace.Add(new TraceEntry(now, now, "Récolte", "Récolter", TraceKind.Entered, TraceOutcome.None));
        bot.Trace.Add(new TraceEntry(now.AddSeconds(1), now, "Récolte", "Récolter", TraceKind.Weighed, TraceOutcome.DoesNotHold) { Watch = new ActivityWatch(WatchedGauge.Pods, 90, WatchAction.Bank) });
        bot.Trace.Add(new TraceEntry(now.AddSeconds(2), now, "Récolte", "Récolter", TraceKind.Played, TraceOutcome.Played) { Node = LocalizedText.Invariant("Récolter le frêne") });

        var runner = await TestHosts.RunningAsync(_store.Host);
        var tabs = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            var texts = new Dictionary<BottomTab, List<string>>();

            main.Refresh();
            window.Show();

            foreach (var tab in Enum.GetValues<BottomTab>())
            {
                main.Bottom.ShowCommand.Execute(tab);
                main.Refresh();
                Dispatcher.UIThread.RunJobs();

                var dock = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "BottomDock");

                texts[tab] = Texts(dock);
                Drawings.DrawPart(dock, "part-bottom-" + tab.ToString().ToLowerInvariant());
            }

            localizer.Use(Localizer.English);
            window.Close();

            return texts;
        });

        Assert.Contains("Avertissements", tabs[BottomTab.Journal]);
        Assert.Contains("Marche vers la case 12", tabs[BottomTab.Journal]);
        Assert.Contains("Pods ≥ 90 % → Banque", tabs[BottomTab.Trace]);
        Assert.Contains("Récolter le frêne", tabs[BottomTab.Trace]);
        Assert.Contains("Joué", tabs[BottomTab.Trace]);
        Assert.Contains("Frigost.profile.json ne se lit pas", tabs[BottomTab.Problems]);
        Assert.Contains("Le profil de Mule a disparu", tabs[BottomTab.Problems]);
        Assert.Contains("Choisir un profil", tabs[BottomTab.Problems]);
        Assert.Contains("En direct", tabs[BottomTab.Protocol]);
        Assert.Contains("Envoyés", tabs[BottomTab.Protocol]);
        Assert.Contains("Ortie", tabs[BottomTab.Statistics]);
        Assert.All(tabs.Values.SelectMany(texts => texts), text => Assert.False(text.StartsWith('[') && text.EndsWith(']'), text));
    }
}
