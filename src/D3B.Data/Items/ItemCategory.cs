namespace D3B.Data.Items;

/// <summary>A category of item types, as <see cref="ItemType.CategoryId"/> holds it: the tabs of the game's inventory (D552).</summary>
/// <remarks>
/// Measured on 3.7.14.18 (client.md, "Items"): the client names no category, so D3B names them by the types each holds. Category 4,
/// the hidden objects such as buffs and the invisible ones, goes into no storage and is not named.
/// </remarks>
public enum ItemCategory
{
    /// <summary>What a character wears: amulets to shields, weapons, pets and mounts, Dofus and trophies.</summary>
    Equipment = 0,

    /// <summary>What a character uses: potions, bread, scrolls, soul stones and nets.</summary>
    Consumable = 1,

    /// <summary>What crafts use: wood, ore, fish, plants and the monsters' drops.</summary>
    Resource = 2,

    /// <summary>Quests' objects.</summary>
    Quest = 3,

    /// <summary>The apparel and costumes that change how a character looks.</summary>
    Cosmetic = 5,
}
