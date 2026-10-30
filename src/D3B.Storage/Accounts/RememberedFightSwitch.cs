using D3B.Bot.Runtime;
using Microsoft.Extensions.Hosting;

namespace D3B.Storage.Accounts;

/// <summary>Gives each bot made the switch "The AI fights" its character was left with (D213).</summary>
/// <remarks>
/// The switch is the character's, kept from one run of the host to the next: a bot made for a character whose switch was
/// left on has the AI play its fights from its first. The host's <c>Fights:Alone</c> turns every bot's on, whatever was kept.
/// Raised on the relay's read loop by <see cref="Bots.Added"/>, it reads the book's memory and never waits.
/// </remarks>
internal sealed class RememberedFightSwitch : IHostedService
{
    private readonly Bots _bots;
    private readonly AccountBook _accounts;

    public RememberedFightSwitch(Bots bots, AccountBook accounts)
    {
        _bots = bots;
        _accounts = accounts;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _bots.Added += OnAdded;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _bots.Added -= OnAdded;

        return Task.CompletedTask;
    }

    private void OnAdded(object? sender, BotActor bot)
    {
        if (_accounts.Find(bot.Key) is { FightsAlone: true })
            bot.FightsAlone = true;
    }
}
