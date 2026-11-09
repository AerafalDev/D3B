using System.Runtime.InteropServices;

namespace D3B.Data.Hints;

/// <summary>A category of the client's hints, such as workshops or dungeons: its identifier and the text that names it (D414).</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct HintCategory
{
    /// <summary>Gets the identifier, as a hint names it.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="HintCategory"/> struct.</summary>
    /// <param name="id">The category.</param>
    /// <param name="nameId">The text naming it.</param>
    public HintCategory(int id, int nameId)
    {
        Id = id;
        NameId = nameId;
    }
}
