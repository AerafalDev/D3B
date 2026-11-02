using MemoryPack;

namespace D3B.Data.Icons;

/// <summary>The icons the pack holds: those of the items, of the professions, of the classes' spells, of the monsters a hunter meets, of the classes' heads and the client's glyphs (D126, D323, D336).</summary>
/// <remarks>
/// <para>
/// Items, jobs, spells and monsters number their icons apart, so each kind has its table, each its own size: items' are
/// 32 pixels square, the client's halved (D265), professions' and monsters' 64, spells' 48, the client's at 1x. Each table
/// is keyed by the client's identifier of the picture, which an item, a job, a spell or a monster names: monsters share
/// pictures, an archmonster its monster's, so that a monster's icon is found through its
/// <see cref="D3B.Data.Monsters.Monster.GfxId"/>.
/// </para>
/// <para>
/// The pack keeps every item's icon the client holds (D265); those of the breeds' spells and their variants, not every
/// spell's; and those of the monsters a sub-area lists, but those hidden from the bestiary or that are bombs, not every
/// monster's: one without an icon is drawn without it. From format 9, the heads of every breed and sex, 64 pixels, keyed
/// as the client names them, <c>Head_&lt;breed&gt;&lt;sex&gt;</c>, breed times 10 plus 1 for a female (<see cref="HeadOf"/>);
/// and the client's white monochrome glyphs D3B draws, 64 pixels, keyed by <see cref="GameGlyph"/>.
/// One entry, read whole the first time an icon is asked for.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class IconCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "icons/catalog";

    /// <summary>Gets the items' icons, by <see cref="D3B.Data.Items.Item.IconId"/>.</summary>
    public IconTable Items { get; }

    /// <summary>Gets the professions' icons, by <see cref="D3B.Data.Jobs.Job.IconId"/>.</summary>
    public IconTable Jobs { get; }

    /// <summary>Gets the spells' icons, by <see cref="D3B.Data.Spells.Spell.IconId"/>.</summary>
    public IconTable Spells { get; }

    /// <summary>Gets the monsters' icons, by <see cref="D3B.Data.Monsters.Monster.GfxId"/>.</summary>
    public IconTable Monsters { get; }

    /// <summary>Gets the classes' heads, by <see cref="HeadOf"/>.</summary>
    public IconTable Heads { get; }

    /// <summary>Gets the client's glyphs, white on transparent, by <see cref="GameGlyph"/>.</summary>
    public IconTable Glyphs { get; }

    /// <summary>Initializes a new instance of the <see cref="IconCatalog"/> class.</summary>
    /// <param name="items">The items' icons.</param>
    /// <param name="jobs">The professions' icons.</param>
    /// <param name="spells">The spells' icons.</param>
    /// <param name="monsters">The monsters' icons.</param>
    /// <param name="heads">The classes' heads.</param>
    /// <param name="glyphs">The client's glyphs.</param>
    [MemoryPackConstructor]
    public IconCatalog(IconTable items, IconTable jobs, IconTable spells, IconTable monsters, IconTable heads, IconTable glyphs)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(heads);
        ArgumentNullException.ThrowIfNull(glyphs);

        Items = items;
        Jobs = jobs;
        Spells = spells;
        Monsters = monsters;
        Heads = heads;
        Glyphs = glyphs;
    }

    /// <summary>Gets the key of a class's head, as the client numbers its sprites.</summary>
    /// <param name="breedId">The breed's id.</param>
    /// <param name="isFemale">Whether the character is female.</param>
    /// <returns>The head's key in <see cref="Heads"/>.</returns>
    public static int HeadOf(int breedId, bool isFemale)
    {
        return (breedId * 10) + (isFemale ? 1 : 0);
    }
}
