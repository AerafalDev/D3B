using System.Collections.Frozen;
using D3B.Data.Monsters;
using D3B.Data.World;

namespace D3B.Bot.Fighting;

/// <summary>Where the fighting activity hunts: the sub-areas, chosen or chosen for the character's level, and their maps a character travels to (D236).</summary>
/// <remarks>
/// The sub-areas a monster lives in are the client's sub-areas' lists, in the pack since format 7. Chosen for the character,
/// the place is the sub-area whose monsters are worth the most at its level, each 20 levels below the character halving a
/// monster's worth and one above it worth nothing, as the harvest's place is chosen (D166); its maps are those the world
/// graph reaches.
/// </remarks>
public sealed class HuntingGround
{
    /// <summary>The levels below the character that halve a monster's worth, as D166's.</summary>
    public const int HalvingLevels = 20;

    /// <summary>Gets the sub-areas hunted in.</summary>
    public IReadOnlyList<int> SubAreas { get; }

    /// <summary>Gets the maps hunted on, by their sub-area.</summary>
    public FrozenDictionary<long, int> Maps { get; }

    private HuntingGround(IReadOnlyList<int> subAreas, FrozenDictionary<long, int> maps)
    {
        SubAreas = subAreas;
        Maps = maps;
    }

    /// <summary>Gets the ground the settings name, or the one chosen for the character's level.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="atlas">The world's sub-areas and maps.</param>
    /// <param name="graph">The world graph, which says the maps a character travels to.</param>
    /// <param name="monsters">The pack's monsters, at their grades' levels.</param>
    /// <param name="level">The character's level.</param>
    /// <param name="subscribed">Whether the character's account is subscribed, which opens the sub-areas a basic account cannot enter.</param>
    /// <returns>The ground; without a sub-area when none holds a monster worth hunting.</returns>
    public static HuntingGround Of(FightSettings settings, WorldAtlas atlas, WorldGraph graph, MonsterCatalog monsters, int level, bool subscribed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(monsters);

        IReadOnlyList<int> subAreas = settings.Place is FightPlace.Automatic
            ? Best(settings, atlas, monsters, level, subscribed) is { } best ? [best] : []
            : settings.SubAreas.Count > 0 ? settings.SubAreas
            : [.. atlas.SubAreas.ToArray().Where(subArea => subArea.AreaId == settings.Area && subArea.MonsterCount > 0).Select(subArea => subArea.Id)];
        var wanted = subAreas.ToHashSet();
        var maps = atlas.Positions.ToArray()
            .Where(position => wanted.Contains(position.SubAreaId) && graph.Contains(position.MapId))
            .ToFrozenDictionary(position => position.MapId, position => position.SubAreaId);

        return new HuntingGround(subAreas, maps);
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

    private static int? Best(FightSettings settings, WorldAtlas atlas, MonsterCatalog monsters, int level, bool subscribed)
    {
        var excluded = settings.Excluded.ToHashSet();
        var chosen = settings.Mode is FightMode.Chosen ? settings.Monsters.ToHashSet() : null;
        int? best = null;
        var bestWorth = 0.0;
        var bestGap = int.MaxValue;

        foreach (var subArea in atlas.SubAreas.Span)
        {
            if (subArea.MonsterCount is 0 || subArea.Level > level || (!subscribed && !subArea.BasicAccountAllowed))
                continue;

            var worth = 0.0;

            foreach (var monster in atlas.MonstersOf(subArea))
            {
                if (!excluded.Contains(monster) && (chosen is null || chosen.Contains(monster)) && Hunted(monsters, monster))
                    worth += WorthOf(monsters, monster, level);
            }

            // The richest first, then the nearest to the character's level.
            var gap = level - subArea.Level;

            if (worth > bestWorth || (worth > 0 && worth == bestWorth && gap < bestGap))
            {
                best = subArea.Id;
                bestWorth = worth;
                bestGap = gap;
            }
        }

        return best;
    }

    private static bool Hunted(MonsterCatalog monsters, int monsterId)
    {
        // Measured (9g): bombs and what the bestiary hides are never in a map's groups; UseSummonSlot is on most monsters.
        return monsters.TryGetMonster(monsterId, out var monster) && (monster.Properties & (MonsterProperties.UseBombSlot | MonsterProperties.HideInBestiary)) is 0;
    }
}
