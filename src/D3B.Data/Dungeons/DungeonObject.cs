using System.Runtime.InteropServices;

namespace D3B.Data.Dungeons;

/// <summary>An object a dungeon requires to be entered, its key (D520).</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct DungeonObject
{
    /// <summary>Gets the item, by id in the client's tables.</summary>
    public int ItemId { get; }

    /// <summary>Gets how many of it the keeper asks for.</summary>
    public int Quantity { get; }

    /// <summary>Initializes a new instance of the <see cref="DungeonObject"/> struct.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="quantity">How many.</param>
    public DungeonObject(int itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}
