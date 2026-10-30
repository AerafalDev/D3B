using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Profiles;

public sealed class StoredStrategiesTests : IAsyncDisposable
{
    private readonly TestStorage _storage = new(strategies: true);

    private IStrategySource Strategies => _storage.Services.GetRequiredService<IStrategySource>();

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task ACharacterGivenNoStrategyFightsAsStandard()
    {
        await SeenAsync();

        var played = Strategies.Played(TestPlay.Key);

        Assert.Null(_storage.Accounts.Find(TestPlay.Key)?.StrategyId);
        Assert.Equal((StrategyRole.Attacker, StrategyStyle.Balanced, true, false), (played.Role, played.Style, played.Closed, played.OutOfActivity));
    }

    [Fact]
    public async Task ACharacterFightsWithItsStrategyAsLastSaved()
    {
        await SeenAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var testing = await _storage.Profiles.SaveAsync(0, "Testing the AI", Bot.Fights.Strategies.ProfileId, """{"Style":"Offensive","OutOfActivity":true}""", cancellationToken);

        await _storage.Accounts.SetStrategyAsync(TestPlay.Key, testing.Id, cancellationToken);

        var played = Strategies.Played(TestPlay.Key);

        Assert.Equal(testing.Id, _storage.Accounts.Find(TestPlay.Key)?.StrategyId);
        Assert.Equal((StrategyStyle.Offensive, true), (played.Style, played.OutOfActivity));

        _storage.Time.Advance(TimeSpan.FromSeconds(1));
        await _storage.Profiles.SaveAsync(testing.Id, "Testing the AI", Bot.Fights.Strategies.ProfileId, """{"Style":"Prudent","Role":"Healer"}""", cancellationToken);
        played = Strategies.Played(TestPlay.Key);

        Assert.Equal((StrategyStyle.Prudent, StrategyRole.Healer, false), (played.Style, played.Role, played.OutOfActivity));
    }

    [Fact]
    public async Task DeletingAStrategyLeavesItsCharactersWithStandard()
    {
        await SeenAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var testing = await _storage.Profiles.SaveAsync(0, "Testing the AI", Bot.Fights.Strategies.ProfileId, """{"OutOfActivity":true}""", cancellationToken);

        await _storage.Accounts.SetStrategyAsync(TestPlay.Key, testing.Id, cancellationToken);
        await _storage.Profiles.DeleteAsync(testing.Id, cancellationToken);

        Assert.Null(_storage.Accounts.Find(TestPlay.Key)?.StrategyId);
        Assert.False(Strategies.Played(TestPlay.Key).OutOfActivity);
    }

    [Fact]
    public async Task AStrategyIsNeverStartedOnABot()
    {
        await SeenAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var testing = await _storage.Profiles.SaveAsync(0, "Testing the AI", Bot.Fights.Strategies.ProfileId, "{}", cancellationToken);
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var start = _storage.Starter.Start(bot, testing);

        Assert.Equal(ProfileStartFailure.ActivityMissing, start.Failure);
        Assert.Null(bot.Activity);
    }

    private async Task SeenAsync()
    {
        await _storage.StartedAsync();

        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(new CharacterListEvent { Characters = { TestPlay.Chosen().Success.Character } });
        await _storage.RecordedAsync();
    }
}
