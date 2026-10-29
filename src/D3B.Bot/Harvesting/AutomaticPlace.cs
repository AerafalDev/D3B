using D3B.Bot.Interactives;
using D3B.Data.World;

namespace D3B.Bot.Harvesting;

/// <summary>Where the harvesting activity harvests for a character when it chooses the place: the sub-area richest in what the character can harvest at its levels (D166).</summary>
/// <remarks>
/// <para>
/// A sub-area counts, for each resource the character can harvest at its profession's level, the maps the host's learned
/// graphics place it on (D138), each as much as the resource is worth: a resource counts half as much every
/// <see cref="HalvingLevels"/> levels below its profession's, so that the zone follows the character as it rises. While
/// no learned graphic places anything the character can harvest, each sub-area counts the worth of the resources it lists.
/// </para>
/// <para>
/// A sub-area above the character's level is left out when the settings ask it, its monsters attacking (D144). Measured on
/// the pack of 3.7.12.12 with the owner's 109 graphics learned, about 10 ms a ranking once the graphics' index is built: a
/// lumberjack of 20 of a character of 60 goes to the Forêt d'Astrub, 69 maps; one of 80 of a character of 200 to the Bois
/// de Litneg, 62; an alchemist of 80 to the Dédale du Dark Vlad; a farmer of 40 to the Champs de Cania; a miner of 1 to the
/// Souterrains.
/// </para>
/// </remarks>
public static class AutomaticPlace
{
    /// <summary>The levels below its profession's for which a resource counts half as much.</summary>
    public const int HalvingLevels = 20;

    /// <summary>Ranks the sub-areas where a character may harvest, the richest first.</summary>
    /// <param name="targets">The skills the settings use (<see cref="HarvestZone.TargetsOf"/>).</param>
    /// <param name="level">The character's level; 0 when unknown, which leaves no sub-area out for its level.</param>
    /// <param name="jobLevels">Its professions' levels, by profession; one not given counts as the first level.</param>
    /// <param name="safeLevel">Whether to leave out the sub-areas above the character's level.</param>
    /// <param name="atlas">The atlas, placing the maps in their sub-areas.</param>
    /// <param name="graph">The world graph, holding the maps a character may travel to.</param>
    /// <param name="places">Where each graphic is drawn.</param>
    /// <param name="graphics">What the host learned of the graphics.</param>
    /// <returns>The sub-areas holding something the character can harvest, the richest first, then the lowest, then by id.</returns>
    public static IReadOnlyList<PlaceCandidate> Rank(
        IEnumerable<HarvestTarget> targets,
        int level,
        IReadOnlyDictionary<int, int> jobLevels,
        bool safeLevel,
        WorldAtlas atlas,
        WorldGraph graph,
        GraphicPlaces places,
        GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(jobLevels);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(graphics);

        // What the character can harvest now, by resource: its skills, and its worth at the profession's level.
        var worths = new Dictionary<int, double>();
        var skills = new Dictionary<int, HashSet<int>>();

        foreach (var target in targets)
        {
            var jobLevel = jobLevels.TryGetValue(target.JobId, out var known) && known > 0 ? known : 1;

            if (target.Level > jobLevel)
                continue;

            var worth = Math.Pow(2, (double)(target.Level - jobLevel) / HalvingLevels);

            worths[target.ItemId] = Math.Max(worths.GetValueOrDefault(target.ItemId), worth);

            if (!skills.TryGetValue(target.ItemId, out var gathering))
                skills[target.ItemId] = gathering = [];

            gathering.Add(target.SkillId);
        }

        var maps = new Dictionary<int, int>();
        var allowed = new HashSet<int>();

        foreach (var subArea in atlas.SubAreas.Span)
        {
            if (!safeLevel || level <= 0 || subArea.Level <= level)
                allowed.Add(subArea.Id);
        }

        foreach (var position in atlas.Positions.Span)
        {
            if (allowed.Contains(position.SubAreaId) && graph.Contains(position.MapId))
                maps[position.SubAreaId] = maps.GetValueOrDefault(position.SubAreaId) + 1;
        }

        var scores = new Dictionary<int, double>();

        foreach (var (item, gathering) in skills)
        {
            foreach (var map in places.MapsOffering(gathering, graphics))
            {
                if (atlas.TryGetPosition(map, out var position) && maps.ContainsKey(position.SubAreaId))
                    scores[position.SubAreaId] = scores.GetValueOrDefault(position.SubAreaId) + worths[item];
            }
        }

        if (scores.Count is 0)
        {
            foreach (var subArea in atlas.SubAreas.Span)
            {
                if (!maps.ContainsKey(subArea.Id))
                    continue;

                foreach (var item in atlas.HarvestablesOf(subArea))
                {
                    if (worths.TryGetValue(item, out var worth))
                        scores[subArea.Id] = scores.GetValueOrDefault(subArea.Id) + worth;
                }
            }
        }

        return [.. scores
            .Select(score => new PlaceCandidate(score.Key, score.Value, maps[score.Key]))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => atlas.TryGetSubArea(candidate.SubAreaId, out var subArea) ? subArea.Level : 0)
            .ThenBy(candidate => candidate.SubAreaId)];
    }
}
