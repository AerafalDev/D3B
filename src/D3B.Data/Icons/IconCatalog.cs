using MemoryPack;

namespace D3B.Data.Icons;

/// <summary>The icons the pack holds: those of the items a harvest names, of the professions, and of the classes' spells (D126).</summary>
/// <remarks>
/// Items, jobs and spells number their icons apart, so each kind has its table, each its own size: items' and
/// professions' icons are 64 pixels square, spells' 48, the client's at 1x. The pack keeps the icons of the items the
/// sub-areas list as harvestable and of those a skill gathers, not every item's, and those of the breeds' spells and
/// their variants, not every spell's: an item or a spell without one is drawn without it. One entry, read whole the
/// first time an icon is asked for.
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

    /// <summary>Initializes a new instance of the <see cref="IconCatalog"/> class.</summary>
    /// <param name="items">The items' icons.</param>
    /// <param name="jobs">The professions' icons.</param>
    /// <param name="spells">The spells' icons.</param>
    [MemoryPackConstructor]
    public IconCatalog(IconTable items, IconTable jobs, IconTable spells)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(spells);

        Items = items;
        Jobs = jobs;
        Spells = spells;
    }
}
