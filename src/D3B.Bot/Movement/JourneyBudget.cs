namespace D3B.Bot.Movement;

/// <summary>How many map changes a journey may make before it is given up, so that a character sent round in circles stops (D74).</summary>
/// <remarks>
/// Twice the changes of the first path and some to spare. A teleport that fails gives the path on foot, which may be many times
/// longer than the one with the teleport: the budget is then made again from the new path, or a character that cannot pay a
/// zaap would give its journey up after a dozen maps (D405).
/// </remarks>
internal sealed class JourneyBudget
{
    private const int SpareChanges = 10;

    private int? _most;

    /// <summary>Fixes the budget from a path, unless one is fixed already.</summary>
    /// <param name="changes">The changes the journey made so far.</param>
    /// <param name="steps">The steps of the path found.</param>
    public void Open(int changes, int steps)
    {
        _most ??= changes + (2 * steps) + SpareChanges;
    }

    /// <summary>Forgets the budget, to fix it again from the next path.</summary>
    public void Renew()
    {
        _most = null;
    }

    /// <summary>Tells whether the journey made all the changes it may.</summary>
    /// <param name="changes">The changes the journey made so far.</param>
    /// <returns>Whether it is over.</returns>
    public bool IsSpent(int changes)
    {
        return changes >= _most;
    }
}
