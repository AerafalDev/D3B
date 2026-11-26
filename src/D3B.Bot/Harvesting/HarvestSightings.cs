using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Harvesting;

/// <summary>Which resources each map holds, as the host's bots saw them: one per host, which every bot shares (D128).</summary>
/// <remarks>
/// The pack does not say which maps hold which resources: the server says what each element of a map is as a character
/// enters it (client.md). A bot tells what it saw (<see cref="Seen"/>), harvested resources included, since they grow
/// back; a tour leaves out the maps seen holding none of its resources. D3B's database keeps each map's elements from one
/// run of the host to the next, not whether they offered their skills (D584): measured on the owner's four harvesters of 26
/// November, 227 journeys of a session arrived on maps the same bot had found empty in an earlier one of the day.
/// </remarks>
public sealed class HarvestSightings
{
    private readonly ConcurrentDictionary<long, MapSighting> _maps = new();

    /// <summary>Gets how many maps the host's bots saw.</summary>
    public int Count => _maps.Count;

    /// <summary>Raised when what a map holds is learned or changes, its elements or its resources, with the map: <see cref="TryGet"/> says what it is now.</summary>
    /// <remarks>Not for an element harvested or grown back. Raised on the bot's thread: a handler must not wait.</remarks>
    public event Action<long>? Learned;

    /// <summary>Remembers what a map holds.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="resources">The resources its elements gather, by item.</param>
    /// <param name="at">When, by the bot's clock.</param>
    public void Seen(long mapId, IEnumerable<int> resources, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _maps[mapId] = new MapSighting(resources.ToFrozenSet(), at, []);
    }

    /// <summary>Takes back what a map held, such as what the database kept, without raising <see cref="Learned"/>, unless a bot saw it since.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elements">Its elements that gather a resource, each as offering its skill: whether they did is not kept.</param>
    /// <param name="at">When a bot saw it holding them.</param>
    public void Remember(long mapId, IReadOnlyCollection<SightedElement> elements, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(elements);

        _maps.TryAdd(mapId, new MapSighting(elements.Select(element => element.ItemId).ToFrozenSet(), at, [.. elements]));
    }

    /// <summary>Remembers what a map holds, each of its elements as it was, which says what still grows back there (D145).</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elements">Its elements that gather a resource.</param>
    /// <param name="at">When, by the bot's clock.</param>
    /// <remarks>
    /// An element harvested by another, unseen, was harvested before a bot first saw it so: each keeps when that was, so that
    /// the next sightings do not put off when it should grow back (9g5).
    /// </remarks>
    public void SeenElements(long mapId, IReadOnlyCollection<SightedElement> elements, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var before = _maps.TryGetValue(mapId, out var previous) && !previous.Elements.IsDefault ? previous : null;
        var kept = elements.Select(element => element with { DisabledSince = element.IsEnabled ? null : DisabledSince(before, element, at) });

        _maps[mapId] = new MapSighting(elements.Select(element => element.ItemId).ToFrozenSet(), at, [.. kept]);

        if (before is null || !SameElements(before, elements))
            Learned?.Invoke(mapId);
    }

    /// <summary>Finds what a map was seen holding.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="sighting">What it held, when it was seen.</param>
    /// <returns>Whether a bot of the host saw it.</returns>
    public bool TryGet(long mapId, [NotNullWhen(true)] out MapSighting? sighting)
    {
        return _maps.TryGetValue(mapId, out sighting);
    }

    /// <summary>Tells whether a map was seen holding none of some resources.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="wanted">The resources, by item.</param>
    /// <returns>Whether a bot saw it, holding none of them.</returns>
    public bool IsEmptyOf(long mapId, IReadOnlySet<int> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        return _maps.TryGetValue(mapId, out var sighting) && !sighting.Resources.Overlaps(wanted);
    }

    private static bool SameElements(MapSighting before, IReadOnlyCollection<SightedElement> elements)
    {
        return before.Elements.Length == elements.Count
            && before.Elements.Select(Pair).ToHashSet().SetEquals(elements.Select(Pair));

        static (int ElementId, int SkillId, int ItemId) Pair(SightedElement element)
        {
            return (element.ElementId, element.SkillId, element.ItemId);
        }
    }

    private static DateTimeOffset DisabledSince(MapSighting? before, SightedElement element, DateTimeOffset at)
    {
        if (before is null)
            return at;

        foreach (var seen in before.Elements)
        {
            if (seen.ElementId == element.ElementId && seen.SkillId == element.SkillId && !seen.IsEnabled)
                return seen.DisabledSince ?? before.At;
        }

        return at;
    }
}
