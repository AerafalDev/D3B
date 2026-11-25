namespace D3B.Engine.Tactics;

/// <summary>A search's time from its start, read on its limits' clock, which every pass of the search shares (D509).</summary>
/// <remarks>
/// The search's casts are weighed within the first three quarters of its budget, and its ends against the enemies' reply
/// within the whole: measured on the owner's captures of 24 November, the reply weighed three fifths of a search's time,
/// with no budget, and the casts cut at half the budget planned 0.4% less than at three quarters.
/// </remarks>
internal sealed class SearchClock
{
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _budget;

    /// <summary>Gets how long the search has lasted.</summary>
    public TimeSpan Elapsed => _clock.GetElapsedTime(_started);

    /// <summary>Gets whether the search's casts have had their share of the budget.</summary>
    public bool CastsAreOver => Elapsed >= _budget * 3 / 4;

    /// <summary>Gets whether the search has had its budget.</summary>
    public bool IsOver => Elapsed >= _budget;

    public SearchClock(TurnSearchLimits limits)
    {
        _clock = limits.Clock;
        _started = _clock.GetTimestamp();
        _budget = limits.Budget;
    }
}
