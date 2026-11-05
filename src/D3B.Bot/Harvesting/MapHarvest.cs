using System.Collections.Frozen;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Runtime;
using D3B.Engine.Maps;

namespace D3B.Bot.Harvesting;

/// <summary>Harvesting the map the character stands on: which element to take next, and a harvest of the whole map (D127, D384).</summary>
/// <remarks>
/// The activity chooses its next element with this and goes on through its zone; a route's action (D384) harvests what the
/// map offers of its list and goes on to the route's next step, the route and not the zone deciding where.
/// </remarks>
internal static class MapHarvest
{
    // An element the server keeps answering "busy" to is left, so that the harvest does not spin on it.
    private const int MostBusy = 3;

    // Measured (D75): the objects come within milliseconds of the harvest's end, in no set order.
    private static readonly TimeSpan _objectsDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);

    /// <summary>Gets a character's level in a profession.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="jobId">The profession.</param>
    /// <returns>The level; one the server has not described yet counts as the first, every character having every profession.</returns>
    public static int LevelOf(CharacterState state, int jobId)
    {
        return state.Jobs.TryGetValue(jobId, out var job) && job.Level > 0 ? job.Level : 1;
    }

    /// <summary>Counts what a character carries of an item, worn items left out.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>How many.</returns>
    public static int Carried(CharacterState state, int itemId)
    {
        return state.Inventory.Values.Where(stack => stack.ItemId == itemId && !stack.IsWorn).Sum(stack => stack.Quantity);
    }

    /// <summary>Lists the elements of the map the character can harvest, the first to take first.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="state">The character's state.</param>
    /// <param name="skipped">The elements left for the visit: taken by another, refused or out of reach; the list adds those it cannot reach.</param>
    /// <returns>The elements with the target each is harvested for: the resource asked for first, then the nearest.</returns>
    public static List<(int ElementId, HarvestTarget Target)> Candidates(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CharacterState state, HashSet<int> skipped)
    {
        var found = new List<(int ElementId, HarvestTarget Target, int Steps)>();

        if (state.Cell is not { } cell || !context.Maps.TryGet(state.MapId!.Value, out var map))
            return [];

        var cells = map.Cells.Span;

        foreach (var element in state.Elements.Values)
        {
            if (skipped.Contains(element.ElementId))
                continue;

            HarvestTarget? best = null;

            foreach (var skill in element.EnabledSkills)
            {
                if (targets.TryGetValue(skill.SkillId, out var target) && LevelOf(state, target.JobId) >= target.Level
                    && (best is null || target.Priority < best.Value.Priority))
                {
                    best = target;
                }
            }

            if (best is not { } chosen)
                continue;

            if (!ElementUse.TryGetCell(map, element.ElementId, out var elementCell)
                || !CellPathfinder.TryFindNearest(cells, cell, [.. ElementCells.UsedFrom(cells, elementCell, cells[cell].Zone)], out var path))
            {
                skipped.Add(element.ElementId);

                continue;
            }

            found.Add((element.ElementId, chosen, path.Steps));
        }

        return [.. found.OrderBy(candidate => candidate.Target.Priority).ThenBy(candidate => candidate.Steps).ThenBy(candidate => candidate.ElementId).Select(candidate => (candidate.ElementId, candidate.Target))];
    }

    /// <summary>Harvests every element of the map that the targets name, one after the other, until none is left.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="targets">The skills wanted, by id.</param>
    /// <param name="cancellationToken">The behavior's token: a fight that begins cuts it, and the route plays the action again once it is over.</param>
    /// <returns>How many elements it harvested.</returns>
    public static async Task<int> HarvestAsync(BotContext context, FrozenDictionary<int, HarvestTarget> targets, CancellationToken cancellationToken)
    {
        var skipped = new HashSet<int>();
        var harvested = 0;
        var busy = 0;

        while (true)
        {
            var state = context.State;

            if (state.IsBusy || state.MapId is null)
            {
                await context.WaitUntilAsync(waited => !waited.IsBusy && waited.MapId is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

                if (context.State.IsBusy || context.State.MapId is null)
                    return harvested;

                continue;
            }

            var candidates = Candidates(context, targets, state, skipped);

            if (candidates.Count is 0)
                return harvested;

            var (elementId, target) = candidates[0];
            var before = Carried(state, target.ItemId);

            switch (await context.UseAsync(elementId, target.SkillId, cancellationToken).ConfigureAwait(false))
            {
                case UseOutcome.Used:
                    await context.WaitUntilAsync(after => Carried(after, target.ItemId) != before, _objectsDelay, cancellationToken).ConfigureAwait(false);
                    harvested++;
                    break;
                case UseOutcome.Busy when ++busy <= MostBusy:
                    break;
                default:
                    skipped.Add(elementId);
                    busy = 0;
                    break;
            }
        }
    }
}
