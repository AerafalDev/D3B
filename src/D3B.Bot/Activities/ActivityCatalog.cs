using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Activities;

/// <summary>The activities the host's modules registered, then those its sources hold now, such as scripts, which the app starts on bots (D92, D108).</summary>
/// <remarks>
/// Registered by the bots module, from every <see cref="ActivityDefinition"/> in the services, in the order they were
/// registered, and every <see cref="IActivitySource"/>. A module's activities are fixed once the host is built; a source's
/// come and go, and <see cref="Version"/> changes with them. A source's activity with the id of a module's, or of an
/// earlier source's, is left out.
/// </remarks>
public sealed class ActivityCatalog
{
    private readonly Dictionary<string, ActivityDefinition> _byId = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ActivityDefinition> _modules;
    private readonly IReadOnlyList<IActivitySource> _sources;

    /// <summary>Gets every activity: the modules' in the order they registered them, then each source's.</summary>
    public IReadOnlyList<ActivityDefinition> All
    {
        get
        {
            if (_sources.Count == 0)
                return _modules;

            var all = new List<ActivityDefinition>(_modules);
            var ids = new HashSet<string>(_byId.Keys, StringComparer.Ordinal);

            foreach (var source in _sources)
                all.AddRange(source.Activities.Where(definition => ids.Add(definition.Id)));

            return all;
        }
    }

    /// <summary>Gets a number that changes whenever a source's activities do, so that a reader lists them again.</summary>
    public int Version => _sources.Sum(source => source.Version);

    /// <summary>Initializes a new instance of the <see cref="ActivityCatalog"/> class.</summary>
    /// <param name="definitions">The modules' activities.</param>
    /// <param name="sources">The sources of activities that come and go, if any.</param>
    /// <exception cref="InvalidOperationException">Two of the modules' activities share an id.</exception>
    public ActivityCatalog(IEnumerable<ActivityDefinition> definitions, IEnumerable<IActivitySource>? sources = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _modules = [.. definitions];
        _sources = [.. sources ?? []];

        foreach (var definition in _modules)
        {
            if (!_byId.TryAdd(definition.Id, definition))
                throw new InvalidOperationException($"Two activities have the id {definition.Id}.");
        }
    }

    /// <summary>Finds an activity by its id.</summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="definition">The activity, when one has the id.</param>
    /// <returns>Whether an activity has the id.</returns>
    public bool TryGet(string id, [NotNullWhen(true)] out ActivityDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_byId.TryGetValue(id, out definition))
            return true;

        foreach (var source in _sources)
        {
            definition = source.Activities.FirstOrDefault(activity => activity.Id == id);

            if (definition is not null)
                return true;
        }

        return false;
    }
}
