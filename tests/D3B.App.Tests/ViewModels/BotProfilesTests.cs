using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class BotProfilesTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task ACardOffersTheProfilesThenTheActivitiesItsCharactersOwnChosen()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack, Walks = 7 }), ct);

        await _store.Assets.SaveProfileAsync("Another", TestStore.StrollId, "{}", ct);
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));
        await _store.Accounts.AssignAsync(TestPlay.Key, walker.Id, ct);
        await _store.Play.EnterAsync(300);

        var page = new BotsViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = Assert.Single(page.Items);

        Assert.Equal(["Another", "Walker", "Harvest", "Stroll"], item.Activities.Select(choice => choice.Name));
        Assert.Equal(["Stroll", "Harvest", "default settings", null], item.Activities.Select(choice => choice.Detail));
        Assert.Equal("Walker", item.SelectedActivity?.Name);

        // A profile saved since is offered at the next refresh, the choice kept.
        await _store.Assets.SaveProfileAsync("Brand new", TestStore.StrollId, "{}", ct);
        page.Refresh(_store.Host.Services);

        Assert.Equal(["Another", "Brand new", "Walker", "Harvest", "Stroll"], item.Activities.Select(choice => choice.Name));
        Assert.Equal("Walker", item.SelectedActivity?.Name);
    }

    [Fact]
    public async Task AProfileStartsFromItsCardWithItsSettings()
    {
        await _store.OpenedAsync();

        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Job = TestReferences.Lumberjack, Walks = 7 }), TestContext.Current.CancellationToken);
        var (_, bot) = await _store.Play.EnterAsync(300);
        var page = new BotsViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = Assert.Single(page.Items);

        item.SelectedActivity = item.Activities.Single(choice => choice.Profile?.Id == walker.Id);
        item.StartCommand.Execute(null);

        Assert.Null(item.Error);
        await bot.Activity!.Completion;
        Assert.Equal(7, Assert.IsType<SettingsActivity>(Assert.Single(_store.Harvests)).Settings.Walks);
        Assert.Equal("Walker", item.ActivityName);
    }

    [Fact]
    public async Task AnActivityWhoseSettingsNeedAValueAsksForAProfile()
    {
        await _store.OpenedAsync();

        var (_, bot) = await _store.Play.EnterAsync(300);
        var page = new BotsViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = Assert.Single(page.Items);

        item.SelectedActivity = item.Activities.Single(choice => choice.Name == "Harvest");
        item.StartCommand.Execute(null);

        Assert.Null(bot.Activity);
        Assert.Equal("Harvest needs settings: make it a profile", item.Error);

        item.SelectedActivity = item.Activities.Single(choice => choice.Name == "Stroll");
        item.StartCommand.Execute(null);

        Assert.IsType<TestActivity>(bot.Activity?.Activity);
        Assert.Null(item.Error);
        Assert.Equal("Stroll", item.ActivityName);
    }

    [Fact]
    public async Task AProfileThatDoesNotStartSaysWhy()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;

        await _store.Assets.SaveProfileAsync("Old", TestStore.HarvestId, """{"Focus":"Dungeon"}""", ct);
        await _store.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Gone", Behavior = "gone" }, ct);
        await _store.Assets.SaveProfileAsync("Empty", TestStore.HarvestId, "{}", ct);

        var (_, bot) = await _store.Play.EnterAsync(300);
        var page = new BotsViewModel(_localizer);

        page.Refresh(_store.Host.Services);

        var item = Assert.Single(page.Items);
        var errors = new List<string?>();

        foreach (var name in new[] { "Old", "Gone", "Empty" })
        {
            item.SelectedActivity = item.Activities.Single(choice => choice.Name == name);
            item.StartCommand.Execute(null);
            errors.Add(item.Error);
        }

        Assert.Null(bot.Activity);
        Assert.Equal(["Old is to review in Profiles", "The behavior of Gone is gone", "The settings of Empty do not hold: see Profiles"], errors);
    }
}
