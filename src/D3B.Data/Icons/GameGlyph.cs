namespace D3B.Data.Icons;

/// <summary>The client's monochrome glyphs the pack keeps, which the studio tints for the game's own concepts (D336).</summary>
/// <remarks>
/// Their values key <see cref="IconCatalog.Glyphs"/> and are D3B's own: the client names its glyphs, never numbers them.
/// A value is never renumbered, so that a pack of the same format reads alike.
/// </remarks>
public enum GameGlyph
{
    /// <summary>No glyph.</summary>
    None = 0,

    /// <summary>A character's life: the client's <c>heart</c>.</summary>
    Life = 1,

    /// <summary>The pods a character carries: the client's <c>weight</c>.</summary>
    Pods = 2,

    /// <summary>A fight: the client's <c>crossedSwords</c>.</summary>
    Fight = 3,

    /// <summary>A profession: the client's <c>job</c>.</summary>
    Profession = 4,

    /// <summary>A harvest, the resources gathered: the client's <c>resources</c>.</summary>
    Harvest = 5,

    /// <summary>Where a character is: the client's <c>pin</c>.</summary>
    Place = 6,

    /// <summary>Time spent: the client's <c>hourglass</c>.</summary>
    Time = 7,

    /// <summary>Kamas: the client's <c>kamas</c>.</summary>
    Kamas = 8,

    /// <summary>Experience: the client's <c>experience</c>.</summary>
    Experience = 9,

    /// <summary>A bank or a chest: the client's <c>chest</c>.</summary>
    Bank = 10,

    /// <summary>The haven bag: the client's <c>havreSac</c>.</summary>
    HavenBag = 11,

    /// <summary>A character: the client's <c>character</c>.</summary>
    Character = 12,

    /// <summary>A pause: the client's <c>pause</c>.</summary>
    Pause = 13,

    /// <summary>Something done: the client's <c>success</c>.</summary>
    Success = 14,

    /// <summary>A zaap: the client's <c>zaap</c>.</summary>
    Zaap = 15,

    /// <summary>A death, a fight lost: the client's <c>skull</c>.</summary>
    Death = 16,
}
