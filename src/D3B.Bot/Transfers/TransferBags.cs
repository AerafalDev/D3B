using System.Collections.Concurrent;
using D3B.Bot.Characters;

namespace D3B.Bot.Transfers;

/// <summary>What each character's transfer carries between its runs, so that a run stopped between a take and its put leaves the next run to put it in (D563).</summary>
/// <remarks>One for the host, kept while it runs: a character plays one run at a time, which alone touches its carry.</remarks>
public sealed class TransferBags
{
    private readonly ConcurrentDictionary<CharacterKey, TransferCarry> _carries = new();

    internal TransferCarry For(CharacterKey character)
    {
        return _carries.GetOrAdd(character, _ => new TransferCarry());
    }
}
