using MemoryPack;

namespace D3B.Data.Icons;

/// <summary>The icons the pack holds: those of the items a harvest names, and those of the professions (D126).</summary>
/// <remarks>
/// Items and jobs number their icons apart, so each kind has its table. The pack keeps the icons of the items the
/// sub-areas list as harvestable and of those a skill gathers, not every item's: an item without one is drawn without
/// it. One entry, read whole the first time an icon is asked for.
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

    /// <summary>Initializes a new instance of the <see cref="IconCatalog"/> class.</summary>
    /// <param name="items">The items' icons.</param>
    /// <param name="jobs">The professions' icons.</param>
    [MemoryPackConstructor]
    public IconCatalog(IconTable items, IconTable jobs)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(jobs);

        Items = items;
        Jobs = jobs;
    }
}
