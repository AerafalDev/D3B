using System.Runtime.InteropServices;

namespace D3B.Data.Npcs;

/// <summary>A non-player character of the client's table: its identifier and the text that names it (D385).</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Npc
{
    /// <summary>Gets the identifier, as the server names the characters standing on a map.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="Npc"/> struct.</summary>
    /// <param name="id">The non-player character.</param>
    /// <param name="nameId">The text naming it.</param>
    public Npc(int id, int nameId)
    {
        Id = id;
        NameId = nameId;
    }
}
