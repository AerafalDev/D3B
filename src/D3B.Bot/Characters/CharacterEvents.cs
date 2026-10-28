using System.Collections.Immutable;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Characters;

/// <summary>Turns what the game connection carries into the character's next state: the bot's state handlers (D9).</summary>
/// <remarks>
/// <para>
/// Each message a side sent gives a new snapshot, or the same one when it says nothing of the character. Pure functions,
/// so they are tested on their own, and a bot's loop is the only caller that keeps their result.
/// </para>
/// <para>
/// A walk, measured (client.md): the server shows the character's path, the client confirms once it arrived, and the
/// server answers; the character then stands on the path's last cell. The server refuses a path that does not start where
/// the character stands, naming its cell. The client stops a walk when the player gives a new path, naming the cell it
/// stopped on.
/// </para>
/// <para>
/// A use, measured (client.md): the server starts it for every actor on the map, disabling the element's skill and
/// stating the element harvested; a harvest ends its duration later, with the objects, the weight and the experience
/// within 2 ms, in no set order. A door's use has no duration: the server names the new map at once. The map's
/// information also lists the elements of the neighbouring maps, marked so, whose updates follow: the state keeps the
/// map's own.
/// </para>
/// </remarks>
public static class CharacterEvents
{
    /// <summary>Gives the character's state once a side sent a message.</summary>
    /// <param name="state">The state before the message.</param>
    /// <param name="side">The side that sent it: the client's requests, the server's events and responses.</param>
    /// <param name="message">The message, readable.</param>
    /// <returns>The state after it.</returns>
    public static CharacterState Apply(CharacterState state, RelayDirection side, IMessage message)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(message);

        return side is RelayDirection.FromClient ? FromClient(state, message) : FromServer(state, message);
    }

    private static CharacterState FromClient(CharacterState state, IMessage message)
    {
        return message switch
        {
            MapMovementCancelRequest cancel when IsCell(cancel.CellId) => state with { Cell = cancel.CellId, Path = [] },
            _ => state,
        };
    }

    private static CharacterState FromServer(CharacterState state, IMessage message)
    {
        var self = state.Key.CharacterId;

        return message switch
        {
            MapCurrentEvent current => state with { MapId = current.MapId, SubAreaId = null, Cell = null, Path = [], Elements = ImmutableSortedDictionary<int, MapElement>.Empty, Use = null },
            MapInformationEvent information when information.MapId == state.MapId => Placed(state, information),
            InteractiveElementUpdatedEvent { Element: { } element } when state.Elements.ContainsKey(element.ElementId) => state with
            {
                Elements = state.Elements.SetItem(element.ElementId, Updated(state.Elements, element)),
            },
            StatedElementUpdatedEvent { Element: { } stated } when state.Elements.ContainsKey(stated.ElementId) => state with
            {
                Elements = state.Elements.SetItem(stated.ElementId, Stated(state.Elements, stated)),
            },
            InteractiveUsedEvent used when used.ActorId == self && used.Duration > 0 => state with { Use = new CurrentUse(used.ElementId, used.SkillId) },
            InteractiveUseEndedEvent ended when state.Use == new CurrentUse(ended.ElementId, ended.SkillId) => state with { Use = null },
            MapMovementEvent movement when movement.ActorId == self && movement.Cells.Count > 0 && movement.Cells.All(IsCell) => state with { Path = [.. movement.Cells] },
            MapMovementConfirmResponse when state.IsWalking => state with { Cell = state.Path[^1], Path = [] },
            MapMovementRefusedEvent refused => state with { Cell = MapGeometry.TryGetCell(new MapPoint(refused.X, refused.Y), out var cell) ? cell : state.Cell, Path = [] },
            InventoryContentEvent content => state with { Inventory = Stacks(content.Objects), Kamas = content.Kamas },
            ObjectAddedEvent { Object: { Item: not null } added } => state with { Inventory = state.Inventory.SetItem(added.Item.Uid, Stack(added)) },
            ObjectQuantityEvent { Object: { } changed } when state.Inventory.TryGetValue(changed.Uid, out var stack) => state with
            {
                Inventory = state.Inventory.SetItem(changed.Uid, stack with { Quantity = changed.Quantity }),
            },
            ObjectsDeletedEvent deleted => state with { Inventory = state.Inventory.RemoveRange(deleted.Uids) },
            KamasUpdateEvent kamas => state with { Kamas = kamas.Kamas },
            InventoryWeightEvent weight => state with { Weight = weight.Weight, MaxWeight = weight.MaxWeight },
            JobDescriptionEvent description => state with { Jobs = Described(state.Jobs, description) },
            JobExperiencesEvent experiences => state with { Jobs = Experienced(state.Jobs, experiences) },
            _ => state,
        };
    }

    private static CharacterState Placed(CharacterState state, MapInformationEvent information)
    {
        var self = information.Actors.FirstOrDefault(actor => actor.Id == state.Key.CharacterId);
        var cell = self?.Disposition is { HasCellId: true } disposition && IsCell(disposition.CellId) ? disposition.CellId : state.Cell;

        // The information also lists the neighbouring maps' elements, which cannot be used from here; their updates follow,
        // which only a known element takes.
        var elements = information.InteractiveElements.Where(element => element.OnCurrentMap).Aggregate(
            ImmutableSortedDictionary<int, MapElement>.Empty,
            (elements, element) => elements.SetItem(element.ElementId, Updated(elements, element)));

        elements = information.StatedElements.Where(stated => stated.OnCurrentMap)
            .Aggregate(elements, (elements, stated) => elements.SetItem(stated.ElementId, Stated(elements, stated)));

        return state with { SubAreaId = information.SubAreaId, Cell = cell, Elements = elements };
    }

    private static MapElement Updated(ImmutableSortedDictionary<int, MapElement> elements, InteractiveElement element)
    {
        var enabled = element.EnabledSkills.Select(Skill).ToImmutableArray();
        var disabled = element.DisabledSkills.Select(Skill).ToImmutableArray();

        return elements.TryGetValue(element.ElementId, out var known)
            ? known with { InteractiveId = element.InteractiveId, EnabledSkills = enabled, DisabledSkills = disabled }
            : new MapElement(element.ElementId, element.InteractiveId, enabled, disabled);
    }

    private static MapElement Stated(ImmutableSortedDictionary<int, MapElement> elements, StatedElement stated)
    {
        var known = elements.TryGetValue(stated.ElementId, out var element) ? element : new MapElement(stated.ElementId, -1, [], []);

        return known with { Cell = IsCell(stated.CellId) ? stated.CellId : null, State = stated.State };
    }

    private static ElementSkill Skill(InteractiveSkill skill)
    {
        return new ElementSkill(skill.SkillId, skill.InstanceId);
    }

    private static ImmutableSortedDictionary<int, InventoryItem> Stacks(IEnumerable<InventoryObject> objects)
    {
        return objects.Where(item => item.Item is not null).Select(Stack).ToImmutableSortedDictionary(stack => stack.Uid, stack => stack);
    }

    private static InventoryItem Stack(InventoryObject item)
    {
        return new InventoryItem(item.Item.Uid, item.Item.ItemId, item.Item.Quantity, item.Position);
    }

    private static ImmutableSortedDictionary<int, CharacterJob> Described(ImmutableSortedDictionary<int, CharacterJob> jobs, JobDescriptionEvent description)
    {
        foreach (var job in description.Jobs)
        {
            var skills = job.Skills.Select(skill => skill.SkillId).ToImmutableArray();

            jobs = jobs.SetItem(job.JobId, jobs.TryGetValue(job.JobId, out var known) ? known with { Skills = skills } : new CharacterJob(job.JobId, 0, 0, 0, 0, skills));
        }

        return jobs;
    }

    private static ImmutableSortedDictionary<int, CharacterJob> Experienced(ImmutableSortedDictionary<int, CharacterJob> jobs, JobExperiencesEvent experiences)
    {
        // The server sends every profession on entering the game, then only the one whose experience changed.
        foreach (var job in experiences.Jobs)
        {
            var skills = jobs.TryGetValue(job.JobId, out var known) ? known.Skills : [];

            jobs = jobs.SetItem(job.JobId, new CharacterJob(job.JobId, job.Level, job.Experience, job.LevelFloor, job.NextLevelFloor, skills));
        }

        return jobs;
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }
}
