using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Storage.Tests.Accounts;

public sealed class RememberedFightSwitchTests : IAsyncDisposable
{
    private readonly TestStorage _storage = new();

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task ABotIsMadeWithTheSwitchItsCharacterWasLeftWith()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(new CharacterListEvent { Characters = { TestPlay.Chosen().Success.Character } });
        await _storage.RecordedAsync();
        await _storage.Accounts.SetFightsAloneAsync(TestPlay.Key, true, cancellationToken);

        var (_, bot) = await _storage.Play.EnterAsync(300);

        Assert.True(_storage.Accounts.Find(TestPlay.Key)?.FightsAlone);
        Assert.True(bot.FightsAlone);
    }

    [Fact]
    public async Task ACharacterWhoseSwitchWasNeverSetHasTheAIPlayNoneOfItsFights()
    {
        await _storage.StartedAsync();

        var (_, bot) = await _storage.Play.EnterAsync(300);

        await _storage.RecordedAsync();

        Assert.False(_storage.Accounts.Find(TestPlay.Key)?.FightsAlone);
        Assert.False(bot.FightsAlone);
    }
}
