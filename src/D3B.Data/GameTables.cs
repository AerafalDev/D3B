using D3B.Data.Banks;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Packs;

namespace D3B.Data;

/// <summary>The game tables of a pack, each read once, when first asked for, then kept.</summary>
public sealed class GameTables
{
    private readonly Lazy<ItemCatalog> _items;
    private readonly Lazy<JobCatalog> _jobs;
    private readonly Lazy<InteractiveCatalog> _interactives;
    private readonly Lazy<BankCatalog> _banks;

    /// <summary>Gets the items and their types.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no items.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public ItemCatalog Items => _items.Value;

    /// <summary>Gets the jobs and the skills.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no jobs.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public JobCatalog Jobs => _jobs.Value;

    /// <summary>Gets the kinds of interactive elements.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no interactives.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public InteractiveCatalog Interactives => _interactives.Value;

    /// <summary>Gets the banks and the bankers.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no banks.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public BankCatalog Banks => _banks.Value;

    /// <summary>Initializes a new instance of the <see cref="GameTables"/> class.</summary>
    /// <param name="pack">The pack holding the tables.</param>
    public GameTables(GamePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        // Publication only: a failed read is not remembered, so that a later call reads again.
        _items = new Lazy<ItemCatalog>(() => pack.Read<ItemCatalog>(ItemCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _jobs = new Lazy<JobCatalog>(() => pack.Read<JobCatalog>(JobCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _interactives = new Lazy<InteractiveCatalog>(() => pack.Read<InteractiveCatalog>(InteractiveCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _banks = new Lazy<BankCatalog>(() => pack.Read<BankCatalog>(BankCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
    }
}
