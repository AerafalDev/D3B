using System.Collections.Concurrent;
using D3B.Bot.Characters;
using D3B.Bot.Movement;

namespace D3B.Samples.Hello;

/// <summary>What the strolls did, for each bot: written by the activity, as its walks end, and read by the page, on the UI's thread.</summary>
internal sealed class StrollJournal
{
    private readonly ConcurrentDictionary<CharacterKey, StrollEntry> _entries = new();

    /// <summary>Gets what the strolls did for a bot.</summary>
    /// <param name="key">The bot's character.</param>
    /// <returns>Its entry, or <see langword="null"/> before its first walk.</returns>
    public StrollEntry? Of(CharacterKey key)
    {
        return _entries.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>Records a walk that ended.</summary>
    /// <param name="key">The bot's character.</param>
    /// <param name="cell">The cell it walked to.</param>
    /// <param name="outcome">How the walk ended.</param>
    public void Record(CharacterKey key, int cell, WalkOutcome outcome)
    {
        var arrived = outcome is WalkOutcome.Arrived ? 1 : 0;

        _entries.AddOrUpdate(
            key,
            _ => new StrollEntry(arrived, cell, outcome),
            (_, entry) => new StrollEntry(entry.Arrivals + arrived, cell, outcome));
    }
}
