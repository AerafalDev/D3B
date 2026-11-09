using System.Collections.Frozen;
using D3B.Bot.Activities;
using D3B.Bot.Movement;
using D3B.Data.Monsters;
using D3B.Data.World;

namespace D3B.Bot.Fighting;

/// <summary>Where the fighting activity hunts: the sub-areas, chosen or chosen for the character's level, and their maps a character travels to (D236).</summary>
/// <remarks>
/// The sub-areas a monster lives in are the client's sub-areas' lists, in the pack since format 7. Chosen for the character,
/// the place is the sub-area whose monsters are worth the most at its level, each 20 levels below the character halving a
/// monster's worth and one above it worth nothing, as the harvest's place is chosen (D166), among those with at least
/// <see cref="LeastMapsShown"/> maps the world graph reaches on a world map, else among the others (D245). Its maps are
/// those the world graph reaches, but the isolated ones when the settings leave them out (D149, D245), and the interiors of a
/// sub-area that also holds maps on a world map, which a tour has no group to fight in (D388).
/// </remarks>
public sealed class HuntingGround
{
    /// <summary>The levels below the character that halve a monster's worth, as D166's.</summary>
    public const int HalvingLevels = 20;

    /// <summary>The least maps on a world map a sub-area holds for the activity to choose it, so that a tour finds groups to fight and does not wait for them (D245).</summary>
    public const int LeastMapsShown = 10;

    /// <summary>Gets the sub-areas hunted in.</summary>
    public IReadOnlyList<int> SubAreas { get; }

    /// <summary>Gets the maps hunted on, by their sub-area: the isolated ones among them only when the settings take them.</summary>
    public FrozenDictionary<long, int> Maps { get; }

    /// <summary>Gets the sub-areas' maps reached from their main part only far round (<see cref="IsolatedMaps"/>), gone through last when the settings take them.</summary>
    public FrozenSet<long> Isolated { get; }

    private HuntingGround(IReadOnlyList<int> subAreas, FrozenDictionary<long, int> maps, FrozenSet<long> isolated)
    {
        SubAreas = subAreas;
        Maps = maps;
        Isolated = isolated;
    }

    /// <summary>Gets the ground the settings name, or the one chosen for the character's level.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="atlas">The world's sub-areas and maps.</param>
    /// <param name="graph">The world graph, which says the maps a character travels to.</param>
    /// <param name="monsters">The pack's monsters, at their grades' levels.</param>
    /// <param name="level">The character's level.</param>
    /// <param name="subscribed">Whether the character's account is subscribed, which opens the sub-areas a basic account cannot enter.</param>
    /// <param name="within">The maps of a zone of the project, when the place is one (D415): the ground is those of them the world graph reaches, all of them, interiors too, since the zone names them.</param>
    /// <returns>The ground; without a sub-area when none holds a monster worth hunting.</returns>
    public static HuntingGround Of(FightSettings settings, WorldAtlas atlas, WorldGraph graph, MonsterCatalog monsters, int level, bool subscribed, IReadOnlySet<long>? within = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(monsters);

        if (within is not null)
            return Named(settings, atlas, graph, within);

        IReadOnlyList<int> subAreas = settings.Place is FightPlace.Automatic
            ? Rank(settings, atlas, graph, monsters, level, subscribed) is [var best, ..] ? [best.SubAreaId] : []
            : settings.SubAreas.Count > 0 ? settings.SubAreas
            : [.. atlas.SubAreas.ToArray().Where(subArea => subArea.AreaId == settings.Area && subArea.MonsterCount > 0).Select(subArea => subArea.Id)];

        return In(settings, atlas, graph, subAreas);
    }

    /// <summary>Gets the ground made of given sub-areas: their maps a character travels to, the isolated ones apart.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="atlas">The world's sub-areas and maps.</param>
    /// <param name="graph">The world graph, which says the maps a character travels to.</param>
    /// <param name="subAreas">The sub-areas.</param>
    /// <returns>The ground; the activity's rotation makes one of each sub-area it draws (D391).</returns>
    public static HuntingGround In(FightSettings settings, WorldAtlas atlas, WorldGraph graph, IReadOnlyList<int> subAreas)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(subAreas);

        var wanted = subAreas.ToHashSet();
        var reached = atlas.Positions.ToArray()
            .Where(position => wanted.Contains(position.SubAreaId) && graph.Contains(position.MapId))
            .ToArray();
        var interiors = SubAreaInteriors.Of(reached);

        // Measured (D388) on 3.7.13.17: of the 444 sub-areas holding monsters, 214 hold maps on a world map and interiors, such as
        // the 22 of Incarnam's fields, where the tour climbed a militia's three floors for no group; the 68 whose maps are all
        // interiors are dungeons, hunted whole.
        var all = reached
            .Where(position => !interiors.Contains(position.MapId))
            .ToDictionary(position => position.MapId, position => position.SubAreaId);
        var isolated = IsolatedMaps.Of(graph, all);
        var maps = all.Where(pair => settings.IsolatedMaps || !isolated.Contains(pair.Key)).ToFrozenDictionary();

        return new HuntingGround(subAreas, maps, isolated);
    }

    private static HuntingGround Named(FightSettings settings, WorldAtlas atlas, WorldGraph graph, IReadOnlySet<long> within)
    {
        var all = atlas.Positions.ToArray()
            .Where(position => within.Contains(position.MapId) && graph.Contains(position.MapId))
            .ToDictionary(position => position.MapId, position => position.SubAreaId);
        var isolated = IsolatedMaps.Of(graph, all);
        var maps = all.Where(pair => settings.IsolatedMaps || !isolated.Contains(pair.Key)).ToFrozenDictionary();

        return new HuntingGround([.. all.Values.Distinct().Order()], maps, isolated);
    }

    /// <summary>Gets what a monster is worth hunting for a character: whole at its level, halved each 20 levels below, nothing above.</summary>
    /// <param name="monsters">The pack's monsters.</param>
    /// <param name="monsterId">The monster.</param>
    /// <param name="level">The character's level.</param>
    /// <returns>Its worth, 0 to 1.</returns>
    public static double WorthOf(MonsterCatalog monsters, int monsterId, int level)
    {
        ArgumentNullException.ThrowIfNull(monsters);

        if (!monsters.TryGetMonster(monsterId, out var monster) || monsters.GradesOf(monster) is not { Length: > 0 } grades)
            return 0;

        int lowest = grades[0].Level, highest = grades[0].Level;

        foreach (var grade in grades)
        {
            lowest = Math.Min(lowest, grade.Level);
            highest = Math.Max(highest, grade.Level);
        }

        if (lowest > level)
            return 0;

        return highest >= level ? 1 : Math.Pow(0.5, (level - highest) / (double)HalvingLevels);
    }

    /// <summary>Ranks the sub-areas the activity may choose for a character, the richest first (D391).</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="atlas">The world's sub-areas and maps.</param>
    /// <param name="graph">The world graph, which says the maps a character travels to.</param>
    /// <param name="monsters">The pack's monsters, at their grades' levels.</param>
    /// <param name="level">The character's level.</param>
    /// <param name="subscribed">Whether the character's account is subscribed, which opens the sub-areas a basic account cannot enter.</param>
    /// <returns>The sub-areas holding a monster worth hunting, those with at least <see cref="LeastMapsShown"/> maps on a world map first, each by its worth, then the nearest to the character's level.</returns>
    public static IReadOnlyList<RotationCandidate> Rank(FightSettings settings, WorldAtlas atlas, WorldGraph graph, MonsterCatalog monsters, int level, bool subscribed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(monsters);

        var excluded = settings.Excluded.ToHashSet();
        var shown = new Dictionary<int, int>();

        // Measured on 3.7.12.12 (9g2): their monsters' worth alone chose a sub-area of one map at level 10, and one of 13 maps
        // on no world map at 150; at least ten maps shown leave each level from 5 to 200 one of 10 to 217 maps, and below 5
        // none, where the best of the others stays.
        foreach (var position in atlas.Positions.Span)
        {
            if (position.WorldMap >= 0 && graph.Contains(position.MapId))
                shown[position.SubAreaId] = shown.GetValueOrDefault(position.SubAreaId) + 1;
        }

        var wide = new List<(int Id, double Worth, int Gap)>();
        var narrow = new List<(int Id, double Worth, int Gap)>();

        foreach (var subArea in atlas.SubAreas.Span)
        {
            if (subArea.MonsterCount is 0 || subArea.Level > level || (!subscribed && !subArea.BasicAccountAllowed))
                continue;

            var worth = 0.0;

            foreach (var monster in atlas.MonstersOf(subArea))
            {
                if (!excluded.Contains(monster) && (settings.Mode is not FightMode.Chosen || ChosenMonsters.Holds(settings, monsters, monster)) && Hunted(monsters, monster))
                    worth += WorthOf(monsters, monster, level);
            }

            if (worth <= 0)
                continue;

            (shown.GetValueOrDefault(subArea.Id) >= LeastMapsShown ? wide : narrow).Add((subArea.Id, worth, level - subArea.Level));
        }

        // The richest first, then the nearest to the character's level.
        return
        [
            .. wide.OrderByDescending(candidate => candidate.Worth).ThenBy(candidate => candidate.Gap).ThenBy(candidate => candidate.Id).Select(candidate => new RotationCandidate(candidate.Id, candidate.Worth)),
            .. narrow.OrderByDescending(candidate => candidate.Worth).ThenBy(candidate => candidate.Gap).ThenBy(candidate => candidate.Id).Select(candidate => new RotationCandidate(candidate.Id, candidate.Worth)),
        ];
    }

    /// <summary>Tells whether a monster is met in a map's groups, which bombs and what the bestiary hides never are.</summary>
    /// <param name="monsters">The pack's monsters.</param>
    /// <param name="monsterId">The monster.</param>
    /// <returns>Whether it is.</returns>
    internal static bool Hunted(MonsterCatalog monsters, int monsterId)
    {
        // Measured (9g): bombs and what the bestiary hides are never in a map's groups; UseSummonSlot is on most monsters.
        return monsters.TryGetMonster(monsterId, out var monster) && (monster.Properties & (MonsterProperties.UseBombSlot | MonsterProperties.HideInBestiary)) is 0;
    }
}
