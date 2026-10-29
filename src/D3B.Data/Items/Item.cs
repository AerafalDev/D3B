using System.Runtime.InteropServices;

namespace D3B.Data.Items;

/// <summary>An item, from the client's <c>ItemsDataRoot</c>: what a bot needs to name it, sort it and weigh it.</summary>
/// <remarks>
/// About 21,500 items, so the struct keeps the client's own widths: 17 bytes, packed and unmanaged, which MemoryPack
/// copies as raw bytes. Effects, recipes, criteria and the client's <c>m_flags</c>, whose bits are not measured, are left
/// out (client.md).
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Item
{
    /// <summary>Gets the item's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets its type, in <see cref="ItemCatalog.Types"/>; the client lacks the type of a few test items.</summary>
    public ushort TypeId { get; }

    /// <summary>Gets its level, from 0 to 200.</summary>
    public byte Level { get; }

    /// <summary>Gets the weight of one, in pods: what it costs of a character's carrying capacity.</summary>
    public ushort Weight { get; }

    /// <summary>Gets its icon, which <see cref="Icons.IconCatalog.Items"/> holds for the items a harvest names, and not for the others.</summary>
    public int IconId { get; }

    /// <summary>Initializes a new instance of the <see cref="Item"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="typeId">Its type.</param>
    /// <param name="level">Its level.</param>
    /// <param name="weight">The weight of one, in pods.</param>
    /// <param name="iconId">Its icon.</param>
    public Item(int id, int nameId, ushort typeId, byte level, ushort weight, int iconId)
    {
        Id = id;
        NameId = nameId;
        TypeId = typeId;
        Level = level;
        Weight = weight;
        IconId = iconId;
    }
}
