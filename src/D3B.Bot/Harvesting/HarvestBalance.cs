using System.Collections.Frozen;

namespace D3B.Bot.Harvesting;

/// <summary>Which professions the activity harvested lately, so that it varies them (<see cref="HarvestOrder.Balanced"/>, D391).</summary>
/// <remarks>
/// Each harvest counts for its profession among the last <see cref="Window"/>: a harvest lasts about the same whatever it
/// gathers (D128), so the count stands for the time a profession had. The profession with the fewest comes first, the order
/// of the settings breaking a tie, which a window that is not full yet makes common.
/// </remarks>
internal sealed class HarvestBalance
{
    /// <summary>How many of the last harvests are counted.</summary>
    public const int Window = 50;

    // Priorities of a profession's resources lie within its own thousand, so that the level orders them inside it.
    private const int Gap = 1000;

    private readonly Queue<int> _recent = new();
    private readonly Dictionary<int, int> _counts = [];
    private readonly IReadOnlyList<int> _jobs;
    private FrozenDictionary<int, HarvestTarget>? _source;
    private FrozenDictionary<int, HarvestTarget>? _prioritised;
    private int _version;
    private int _builtAt = -1;

    /// <summary>Initializes a new instance of the <see cref="HarvestBalance"/> class.</summary>
    /// <param name="jobs">The professions the settings chose, in their order.</param>
    public HarvestBalance(IReadOnlyList<int> jobs)
    {
        _jobs = jobs;
    }

    /// <summary>Counts a harvest.</summary>
    /// <param name="jobId">The profession it was of.</param>
    public void Record(int jobId)
    {
        _recent.Enqueue(jobId);
        _counts[jobId] = _counts.GetValueOrDefault(jobId) + 1;

        if (_recent.Count > Window)
        {
            var old = _recent.Dequeue();

            _counts[old]--;
        }

        _version++;
    }

    /// <summary>Gets the professions, the one harvested least lately first.</summary>
    /// <returns>The professions of the settings, then those harvested that they do not name.</returns>
    public List<int> Order()
    {
        return [.. _jobs.Concat(_counts.Keys).Distinct().Select((job, index) => (job, index)).OrderBy(pair => _counts.GetValueOrDefault(pair.job)).ThenBy(pair => pair.index).Select(pair => pair.job)];
    }

    /// <summary>Gives the skills the priority this balance wants: by profession in its order, then the highest level first.</summary>
    /// <param name="targets">The zone's skills.</param>
    /// <returns>The same skills with their priorities; the same instance while nothing was harvested since.</returns>
    public FrozenDictionary<int, HarvestTarget> Prioritise(FrozenDictionary<int, HarvestTarget> targets)
    {
        if (ReferenceEquals(targets, _source) && _builtAt == _version && _prioritised is { } built)
            return built;

        var ranks = Order().Select((job, index) => (job, index)).ToDictionary(pair => pair.job, pair => pair.index);

        _source = targets;
        _builtAt = _version;

        return _prioritised = targets.ToFrozenDictionary(pair => pair.Key, pair => pair.Value with { Priority = (ranks.GetValueOrDefault(pair.Value.JobId, ranks.Count) * Gap) - pair.Value.Level });
    }
}
