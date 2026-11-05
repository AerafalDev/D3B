namespace D3B.Bot.Assets;

/// <summary>What an asset of the project is (D296).</summary>
/// <remarks>
/// Behaviors and profiles hold what they are made of since session 7b, routes since 7d2; the other kinds are foreseen, read and written
/// back as they are, until the sessions that make them (D306).
/// </remarks>
public enum AssetKind
{
    /// <summary>A behavior: states by priority over short actions (D297).</summary>
    Behavior,

    /// <summary>A profile: a behavior and the values it changes (D300).</summary>
    Profile,

    /// <summary>A route: maps in order, each with its actions (D299); its anchors since 7d2, the actions at 7d3.</summary>
    Route,

    /// <summary>A zone: a set of maps D3B goes through its own way (D299), at 7d.</summary>
    Zone,

    /// <summary>A list of resources, monsters or items, at 7f.</summary>
    List,

    /// <summary>A planning: for a character, which profile when, and when to stop, at 7f.</summary>
    Planning,

    /// <summary>Fight preferences the AI respects (D301), at 7h.</summary>
    FightPreferences,
}
