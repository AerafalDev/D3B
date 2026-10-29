using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.World;

namespace D3B.Bot.Settings;

/// <summary>The game's values the settings name, read from the installed build's pack: what the app offers, and what a check accepts (D98).</summary>
/// <remarks>
/// <para>
/// Professions are every one but the base, which every character has. A profession's resources are the items its skills
/// gather, each at the lowest level a skill gathers it from; a sub-area's level is the pack's.
/// </para>
/// <para>Each list is read once, then shared: the pack does not change while the host runs.</para>
/// </remarks>
public sealed class GameReferences
{
    private readonly WorldAtlas _atlas;
    private readonly Lazy<IReadOnlyList<GameReference>> _jobs;
    private readonly Lazy<IReadOnlyList<GameReference>> _areas;
    private readonly Lazy<IReadOnlyList<GameReference>> _subAreas;
    private readonly Lazy<IReadOnlyList<GameReference>> _resources;
    private readonly Lazy<ILookup<int, GameReference>> _resourcesByJob;

    /// <summary>Initializes a new instance of the <see cref="GameReferences"/> class.</summary>
    /// <param name="atlas">The world's areas and sub-areas.</param>
    /// <param name="jobs">The professions and their skills.</param>
    /// <param name="items">The items, which name the resources.</param>
    public GameReferences(WorldAtlas atlas, JobCatalog jobs, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(items);

        _atlas = atlas;
        _jobs = new Lazy<IReadOnlyList<GameReference>>(() => JobsOf(jobs));
        _areas = new Lazy<IReadOnlyList<GameReference>>(() => [.. atlas.Areas.ToArray().Select(area => new GameReference(area.Id, area.NameId, null))]);
        _subAreas = new Lazy<IReadOnlyList<GameReference>>(() => [.. atlas.SubAreas.ToArray().Select(subArea => new GameReference(subArea.Id, subArea.NameId, subArea.Level))]);
        _resourcesByJob = new Lazy<ILookup<int, GameReference>>(() => ResourcesByJob(jobs, items));
        _resources = new Lazy<IReadOnlyList<GameReference>>(() => [.. _resourcesByJob.Value
            .SelectMany(resources => resources)
            .GroupBy(resource => resource.Id)
            .Select(resource => resource.MinBy(gathered => gathered.Level))
            .OrderBy(resource => resource.Id)]);
    }

    private static List<GameReference> JobsOf(JobCatalog jobs)
    {
        return [.. jobs.Jobs.ToArray().Where(job => job.Id != Job.Base).Select(job => new GameReference(job.Id, job.NameId, null))];
    }

    private static ILookup<int, GameReference> ResourcesByJob(JobCatalog jobs, ItemCatalog items)
    {
        return jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId != Skill.NoItem && skill.JobId != Job.Base)
            .GroupBy(skill => (skill.JobId, skill.GatheredItemId))
            .Select(skills => new
            {
                skills.Key.JobId,
                Resource = new GameReference(
                    skills.Key.GatheredItemId,
                    items.TryGetItem(skills.Key.GatheredItemId, out var item) ? item.NameId : 0,
                    skills.Min(skill => skill.MinLevel)),
            })
            .OrderBy(gathered => gathered.Resource.Level)
            .ThenBy(gathered => gathered.Resource.Id)
            .ToLookup(gathered => gathered.JobId, gathered => gathered.Resource);
    }

    /// <summary>Gets the values a setting of a kind offers.</summary>
    /// <param name="kind">A kind of the game: <see cref="SettingKind.Job"/>, <see cref="SettingKind.Area"/>, <see cref="SettingKind.SubArea"/> or <see cref="SettingKind.Resource"/>.</param>
    /// <param name="within">The area of the sub-areas, or the profession of the resources, to offer those within it alone.</param>
    /// <returns>The values; a profession's resources by the level it gathers them from.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is none of the game's.</exception>
    public IReadOnlyList<GameReference> Options(SettingKind kind, int? within = null)
    {
        return (kind, within) switch
        {
            (SettingKind.Job, _) => _jobs.Value,
            (SettingKind.Area, _) => _areas.Value,
            (SettingKind.SubArea, null) => _subAreas.Value,
            (SettingKind.SubArea, { } area) => [.. _subAreas.Value.Where(subArea => IsWithin(kind, subArea.Id, area))],
            (SettingKind.Resource, null) => _resources.Value,
            (SettingKind.Resource, { } job) => [.. _resourcesByJob.Value[job]],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind names nothing of the game's."),
        };
    }

    /// <summary>Tells whether the game has a value of a kind.</summary>
    /// <param name="kind">A kind of the game.</param>
    /// <param name="id">The value's id.</param>
    /// <returns>Whether it has.</returns>
    public bool Exists(SettingKind kind, int id)
    {
        return kind switch
        {
            SettingKind.Job => _jobs.Value.Any(job => job.Id == id),
            SettingKind.Area => _atlas.TryGetArea(id, out _),
            SettingKind.SubArea => _atlas.TryGetSubArea(id, out _),
            SettingKind.Resource => _resources.Value.Any(resource => resource.Id == id),
            _ => false,
        };
    }

    /// <summary>Tells whether a value of the game is within another: a sub-area within an area, a resource within a profession.</summary>
    /// <param name="kind"><see cref="SettingKind.SubArea"/> or <see cref="SettingKind.Resource"/>.</param>
    /// <param name="id">The value's id.</param>
    /// <param name="within">The area's or the profession's id.</param>
    /// <returns>Whether it is.</returns>
    public bool IsWithin(SettingKind kind, int id, int within)
    {
        return kind switch
        {
            SettingKind.SubArea => _atlas.TryGetSubArea(id, out var subArea) && subArea.AreaId == within,
            SettingKind.Resource => _resourcesByJob.Value[within].Any(resource => resource.Id == id),
            _ => false,
        };
    }
}
