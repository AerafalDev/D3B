using System.Collections.Frozen;
using D3B.Data.Items;

namespace D3B.Bot.Transfers;

/// <summary>Tells the items a transfer moves, from its settings and the pack's items (D550, D552).</summary>
/// <remarks>An item moves when the transfer moves everything, or its category, its type or the item itself is chosen, and neither its type nor itself is excluded; an item the pack lacks never does.</remarks>
internal sealed class TransferFilter
{
    private readonly ItemCatalog _items;
    private readonly bool _everything;
    private readonly FrozenSet<int> _categories;
    private readonly FrozenSet<int> _types;
    private readonly FrozenSet<int> _chosen;
    private readonly FrozenSet<int> _excludedTypes;
    private readonly FrozenSet<int> _excluded;

    public TransferFilter(TransferSettings settings, ItemCatalog items)
    {
        _items = items;
        _everything = settings.Everything;
        _categories = settings.Categories.Select(category => (int)category).ToFrozenSet();
        _types = settings.Types.ToFrozenSet();
        _chosen = settings.Items.ToFrozenSet();
        _excludedTypes = settings.ExcludedTypes.ToFrozenSet();
        _excluded = settings.ExcludedItems.ToFrozenSet();
    }

    public bool Moves(int itemId)
    {
        if (_excluded.Contains(itemId) || !_items.TryGetItem(itemId, out var item) || _excludedTypes.Contains(item.TypeId))
            return false;

        if (_everything || _chosen.Contains(itemId) || _types.Contains(item.TypeId))
            return true;

        return _items.TryGetType(item.TypeId, out var type) && _categories.Contains(type.CategoryId);
    }

    public int WeightOf(int itemId)
    {
        return _items.TryGetItem(itemId, out var item) ? item.Weight : 0;
    }
}
