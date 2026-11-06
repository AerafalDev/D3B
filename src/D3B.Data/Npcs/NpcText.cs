using System.Runtime.InteropServices;

namespace D3B.Data.Npcs;

/// <summary>A question an NPC asks, or a reply it offers, with the text it reads (D385).</summary>
/// <remarks>
/// The client's table pairs each with a text, and the pairing holds for every NPC: measured on 3.7.13.17, no id of 64,360 replies
/// or of 47,652 questions has two texts. Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is
/// part of the pack format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct NpcText
{
    /// <summary>Gets the question's or the reply's identifier, as the server names it in a dialog.</summary>
    public int Id { get; }

    /// <summary>Gets the text it reads, by identifier.</summary>
    public int TextId { get; }

    /// <summary>Initializes a new instance of the <see cref="NpcText"/> struct.</summary>
    /// <param name="id">The question or the reply.</param>
    /// <param name="textId">The text it reads.</param>
    public NpcText(int id, int textId)
    {
        Id = id;
        TextId = textId;
    }
}
