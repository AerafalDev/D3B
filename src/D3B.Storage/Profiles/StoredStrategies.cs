using System.Collections.Concurrent;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Storage.Accounts;

namespace D3B.Storage.Profiles;

/// <summary>Gives each character the strategy D3B's database keeps for it, D3B's defaults when it has none (D233).</summary>
/// <remarks>
/// Reads the books' memory, never the database, and never waits: the bots ask it from their loops. A strategy is read once
/// per save of its profile; one that no longer reads whole, a setting dropped since, plays D3B's defaults for what it lost.
/// </remarks>
internal sealed class StoredStrategies : IStrategySource
{
    private readonly AccountBook _accounts;
    private readonly ProfileBook _profiles;
    private readonly ActivityCatalog _catalog;
    private readonly ConcurrentDictionary<int, (DateTimeOffset Updated, StrategySettings Settings)> _read = new();

    public StoredStrategies(AccountBook accounts, ProfileBook profiles, ActivityCatalog catalog)
    {
        _accounts = accounts;
        _profiles = profiles;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public StrategySettings Played(CharacterKey character)
    {
        if (_accounts.Find(character)?.StrategyId is not { } id
            || _profiles.Find(id) is not { } profile
            || _catalog.Strategy is not { } strategy
            || profile.ActivityId != strategy.Id)
        {
            return new StrategySettings();
        }

        if (_read.TryGetValue(id, out var read) && read.Updated == profile.Updated)
            return read.Settings;

        var settings = strategy.Settings.Read(profile.Settings).Settings as StrategySettings ?? new StrategySettings();

        _read[id] = (profile.Updated, settings);

        return settings;
    }
}
