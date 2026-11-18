using D3B.Bot.Characters;

namespace D3B.Bot.Groups;

/// <summary>A member of a group the one waiting for it does not have with it (D432, D436).</summary>
/// <param name="Key">The member's character.</param>
/// <param name="Name">Its name, as its state last said.</param>
/// <param name="Absence">Why it is missing.</param>
public sealed record MissingMember(CharacterKey Key, string Name, MemberAbsence Absence);
