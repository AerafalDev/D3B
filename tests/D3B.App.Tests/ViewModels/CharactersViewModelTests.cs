using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Properties;
using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Storage.Accounts;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class CharactersViewModelTests : IAsyncDisposable
{
    private static readonly GameAccount _mule = new(42, "Mule", "0042");

    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly Dialogs _dialogs = new();
    private readonly List<CharacterKey> _chosen = [];
    private readonly List<CharacterKey> _opened = [];
    private readonly BotsViewModel _bots;
    private readonly CharacterAccounts _accounts;
    private readonly CharactersViewModel _panel;

    public CharactersViewModelTests()
    {
        _bots = new BotsViewModel(_localizer);
        _accounts = new CharacterAccounts(_localizer, _dialogs, new Toasts());
        _panel = new CharactersViewModel(_localizer, _bots, _accounts, _chosen.Add, _opened.Add);
    }

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task TheCharactersOfflineAreLinesThePlayedLastFirstUntilTheirClientConnects()
    {
        await _store.OpenedAsync();

        var time = _store.Play.Time;
        var ct = TestContext.Current.CancellationToken;

        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));
        await _store.Accounts.SeeAsync(_mule, [new ListedCharacter(80_001, "Minette", 54, 3, true)], time.GetUtcNow(), ct);
        await _store.Accounts.PlayAsync(_mule, new ListedCharacter(80_001, "Minette", 54), time.GetUtcNow(), ct);
        time.Advance(TimeSpan.FromHours(2));
        Refresh();

        Assert.Empty(_panel.Online);
        Assert.Equal(["Minette", "Character"], _panel.Offline.Select(line => line.Name));
        Assert.Equal(("Level 54", "2 h ago"), (_panel.Offline[0].Level, _panel.Offline[0].LastPlayed));
        Assert.Equal("Disconnected", _panel.Offline[1].LastPlayed);
        Assert.Equal(2, _panel.OfflineCount);
        Assert.False(_panel.IsOfflineOpen);
        Assert.False(_panel.IsEmpty);

        // Its breed, which the list gave, is kept though a later list leaves it out.
        Assert.Equal((3, true), (_store.Accounts.Find(new CharacterKey(42, 80_001))?.BreedId, _store.Accounts.Find(new CharacterKey(42, 80_001))?.IsFemale));

        _panel.ToggleOfflineCommand.Execute(null);

        Assert.True(_panel.IsOfflineOpen);

        await _store.Play.EnterAsync(300);
        Refresh();

        var card = Assert.Single(_panel.Online);

        Assert.Equal(TestPlay.Key, card.Bot.Key);
        Assert.Same(_panel, card.Panel);
        Assert.Equal(["Minette"], _panel.Offline.Select(line => line.Name));
        Assert.Equal(PlayState.Stopped, card.PlayState);
        Assert.Equal("Stopped", card.CardActivity);
        Assert.Equal("Sub-area " + TestPlay.SubAreaId, card.Where);
        Assert.Equal($"4,-18 · Sub-area {TestPlay.SubAreaId}, cell 300", card.WhereTip);
    }

    [Fact]
    public async Task AClickChoosesADoubleClickOpensAndTheChosenIsOutlined()
    {
        await _store.OpenedAsync();
        await _store.Play.EnterAsync(300);
        Refresh();

        _panel.Choose(TestPlay.Key);
        _panel.Open(TestPlay.Key);
        _panel.Chosen = TestPlay.Key;
        Refresh();

        Assert.Equal([TestPlay.Key], _chosen);
        Assert.Equal([TestPlay.Key], _opened);
        Assert.True(_panel.Online[0].IsChosen);
        Assert.True(_panel.ChosenIsOnline);
        Assert.False(_panel.ChosenHasProfile);
    }

    [Fact]
    public async Task TheMenuGivesAProfileWhichMayStartOnConnectionAndForgetsAfterAsking()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var stroll = await _store.Assets.SaveProfileAsync("Stroll", TestStore.StrollId, "{}", ct);

        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));
        _panel.Chosen = TestPlay.Key;
        Refresh();

        Assert.Contains(_panel.MyProfiles, choice => choice.Id == stroll.Id && choice.Name == "Stroll");
        Assert.All(_panel.GivenProfiles, choice => Assert.True(choice.IsGiven));

        var version = _store.Accounts.Version;

        _panel.AssignCommand.Execute(_panel.MyProfiles.Single(choice => choice.Id == stroll.Id));
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));
        Refresh();

        Assert.True(_panel.ChosenHasProfile);

        version = _store.Accounts.Version;
        _panel.ToggleStartsOnConnectionCommand.Execute(null);
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));
        Refresh();

        Assert.True(_panel.ChosenStartsOnConnection);
        Assert.Equal((stroll.Id, true), (_store.Accounts.Find(TestPlay.Key)?.Profile, _store.Accounts.Find(TestPlay.Key)?.StartsOnConnection));

        _panel.ForgetCommand.Execute(null);

        var question = Assert.IsType<QuestionViewModel>(_dialogs.Current);

        Assert.True(question.IsDestructive);
        question.CancelCommand.Execute(null);
        Assert.NotNull(_store.Accounts.Find(TestPlay.Key));

        version = _store.Accounts.Version;
        _panel.ForgetCommand.Execute(null);
        Assert.IsType<QuestionViewModel>(_dialogs.Current).ConfirmCommand.Execute(null);
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));
        Refresh();

        Assert.True(_panel.IsEmpty);
    }

    [Fact]
    public async Task TheInspectorShowsACharactersGameVariablesAndAccountAndFiltersThem()
    {
        await _store.OpenedAsync();
        await _store.Accounts.SeeAsync(TestPlay.SubscribedAccount, [new ListedCharacter(TestPlay.CharacterId, "Character", 200, 8, false)], _store.Play.Time.GetUtcNow(), TestContext.Current.CancellationToken);
        await _store.Play.EnterAsync(300);
        Refresh();

        var variables = _store.Host.Services.GetService(typeof(D3B.Studio.Behaviors.CharacterVariables)) as D3B.Studio.Behaviors.CharacterVariables;

        variables!.Set(TestPlay.Key, "banque_faite", System.Text.Json.Nodes.JsonValue.Create(true));
        variables.Set(TestPlay.Key, "tours", System.Text.Json.Nodes.JsonValue.Create(12));

        var opened = new List<string>();
        var inspector = new PropertiesViewModel(
            new CharacterPropertiesViewModel(_localizer, _accounts, key => _bots.Items.FirstOrDefault(bot => bot.Bot.Key == key), _panel.LineOf, opened.Add, _opened.Add),
            new AssetPropertiesViewModel(_localizer));

        Assert.True(inspector.IsEmpty);

        inspector.ShowCharacter(TestPlay.Key);
        inspector.Refresh(_store.Host.Services);

        var character = inspector.Character;

        Assert.False(inspector.IsEmpty);
        Assert.Equal("Character", character.Name);
        Assert.Equal("level 200", character.Subtitle);
        Assert.Equal("No profile", character.Profile?.Name);
        Assert.False(character.HasProfile);
        Assert.Equal(["banque_faite", "tours"], character.Variables.Select(variable => variable.Name));
        Assert.True(character.Variables[0].IsFlag && character.Variables[0].Flag);
        Assert.True(character.Variables[1].IsNumber);
        Assert.Equal("12", character.Variables[1].Text);
        Assert.Equal("nickname#tag", character.Account);
        Assert.True(character.IsSubscribed);

        // An edit sets the character's variable, which its behaviors read.
        character.Variables[1].Text = "13";

        Assert.Equal(13, variables.Get(TestPlay.Key, "tours")!.GetValue<double>());

        inspector.Filter = "tour";

        Assert.False(character.ShowsGame);
        Assert.False(character.ShowsAccountSection);
        Assert.True(character.ShowsVariables);
        Assert.Equal([false, true], character.Variables.Select(variable => variable.IsShown));

        inspector.Filter = "";
        inspector.LabelShare = 0.9;

        Assert.Equal(0.7, inspector.LabelShare);
        Assert.True(character.ShowsGame);

        character.OpenTabCommand.Execute(null);

        Assert.Equal([TestPlay.Key], _opened);
    }

    [Fact]
    public async Task TheInspectorShowsAProfileItsFileWhatItUsesAndWhoPlaysIt()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var stroll = await _store.Assets.SaveProfileAsync("Stroll", TestStore.StrollId, "{}", ct);

        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));
        await _store.Accounts.AssignAsync(TestPlay.Key, stroll.Id, ct);

        var inspector = new PropertiesViewModel(
            new CharacterPropertiesViewModel(_localizer, _accounts, _ => null, _ => null, _ => { }, _ => { }),
            new AssetPropertiesViewModel(_localizer));

        inspector.ShowAsset(stroll.Id);
        inspector.Refresh(_store.Host.Services);

        var asset = inspector.Asset;

        Assert.True(inspector.ShowsAsset);
        Assert.False(inspector.ShowsCharacter);
        Assert.Equal(("Stroll", "Profile"), (asset.Name, asset.KindText));
        Assert.Equal(stroll.Path, asset.File);
        Assert.Equal("1", asset.Format);
        Assert.Single(asset.Uses);
        Assert.Equal(["Character"], asset.UsedBy.Select(use => use.Name));
        Assert.True(asset.UsedBy[0].IsCharacter);
    }

    private void Refresh()
    {
        var services = _store.Host.Services;

        _bots.Refresh(services);
        _panel.Refresh(services);
    }
}
