using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Groups;

/// <summary>The group's chain: its followers' order, who is with the master, and who is missing (D430, D432, D437).</summary>
/// <remarks>Pure functions of the bots' states, so that the master's gate and its followers read the same chain.</remarks>
public static class GroupChain
{
    /// <summary>Orders a master's followers as the chain goes: those in the master's party in the party's order, then the others by name (D430).</summary>
    /// <param name="master">The master's state, whose party gives the order.</param>
    /// <param name="followers">The followers' bots.</param>
    /// <returns>The followers, the first behind the master first.</returns>
    public static IReadOnlyList<BotActor> Order(CharacterState master, IReadOnlyList<BotActor> followers)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(followers);

        var members = master.Party is { } party && party.LeaderId == master.Key.CharacterId ? party.Members : [];

        return
        [
            .. followers
                .OrderBy(follower => members.IndexOf(follower.Key.CharacterId) is var at and >= 0 ? at : int.MaxValue)
                .ThenBy(follower => follower.State.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(follower => follower.Key.CharacterId),
        ];
    }

    /// <summary>Tells whether a member stands with the master: placed on its map, or each in its own haven bag (D437).</summary>
    /// <param name="master">The master's state.</param>
    /// <param name="member">The member's state.</param>
    /// <returns>Whether they are together.</returns>
    /// <remarks>The owner's choice: as the master enters its haven bag, each follower enters its own, so that each leaves by the way the one before it took (D432).</remarks>
    public static bool Together(CharacterState master, CharacterState member)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(member);

        if (IsInOwnHavenBag(master))
            return IsInOwnHavenBag(member);

        return master.MapId is { } map && member.MapId == map && member.Cell is not null && !member.IsInHavenBag;
    }

    /// <summary>Tells whether a character is in its own haven bag.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsInOwnHavenBag(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.HavenBagOwnerId == state.Key.CharacterId;
    }

    /// <summary>Tells why a follower is not with its master, if it is not (D432).</summary>
    /// <param name="master">The master's state.</param>
    /// <param name="follower">The follower's bot.</param>
    /// <returns>Why it is missing, or <see langword="null"/> when it is with its master and in its party.</returns>
    public static MemberAbsence? AbsenceOf(CharacterState master, BotActor follower)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(follower);

        var state = follower.State;

        if (!follower.IsConnected)
            return MemberAbsence.Disconnected;

        if (follower.IsPaused)
            return MemberAbsence.Paused;

        if (state.IsFighting)
            return MemberAbsence.Fighting;

        if (!Together(master, state))
            return MemberAbsence.Away;

        // The owner's choice: in the party too, which the fights locked to it need (D431, D437).
        return master.Party is { } party && party.LeaderId == master.Key.CharacterId && party.Members.Contains(follower.Key.CharacterId)
            ? null
            : MemberAbsence.OutOfParty;
    }

    /// <summary>Gets the followers missing for the master to act, in the chain's order (D432).</summary>
    /// <param name="master">The master's state.</param>
    /// <param name="followers">The followers' bots.</param>
    /// <returns>Each one missing, and why.</returns>
    public static IReadOnlyList<MissingMember> Missing(CharacterState master, IReadOnlyList<BotActor> followers)
    {
        ArgumentNullException.ThrowIfNull(master);

        return
        [
            .. Order(master, followers)
                .Select(follower => (follower, Absence: AbsenceOf(master, follower)))
                .Where(missing => missing.Absence is not null)
                .Select(missing => new MissingMember(missing.follower.Key, missing.follower.State.Name, missing.Absence!.Value)),
        ];
    }
}
