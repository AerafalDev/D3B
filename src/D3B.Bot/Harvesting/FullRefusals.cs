namespace D3B.Bot.Harvesting;

/// <summary>The harvests the server refused in a row while the character carried its pods or more, going on full (D578).</summary>
/// <remarks>
/// A refusal alone says nothing of the pods: another player may have taken the element an instant before. Several in a row,
/// nothing harvested between, are the pods' doing, every element of the zone refused in turn.
/// </remarks>
internal sealed class FullRefusals
{
    /// <summary>The refusals in a row that end the harvest.</summary>
    public const int MostInARow = 3;

    private int _inARow;

    /// <summary>Counts a harvest the server refused while the character was full.</summary>
    /// <returns>Whether the refusals in a row reached <see cref="MostInARow"/>.</returns>
    public bool Refused()
    {
        return ++_inARow >= MostInARow;
    }

    /// <summary>Says a harvest was given, or refused while the character was not full: the refusals start again.</summary>
    public void Reset()
    {
        _inARow = 0;
    }
}
