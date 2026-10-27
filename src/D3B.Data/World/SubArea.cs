using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>A sub-area of the world, from the client's <c>SubAreasDataRoot</c>: what a bot needs to travel to it.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>. Session 2c adds what harvesting needs.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SubArea
{
    /// <summary>The <see cref="AssociatedZaapMapId"/> of a sub-area that names no zaap.</summary>
    public const long NoZaap = 0;

    /// <summary>Gets the sub-area's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the area it belongs to.</summary>
    public int AreaId { get; }

    /// <summary>Gets its level, from 0 to 200.</summary>
    public int Level { get; }

    /// <summary>Gets the map of the zaap the client associates with it, or <see cref="NoZaap"/>.</summary>
    public long AssociatedZaapMapId { get; }

    /// <summary>Gets whether an account without a subscription may enter it.</summary>
    public bool BasicAccountAllowed { get; }

    /// <summary>Initializes a new instance of the <see cref="SubArea"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="areaId">Its area.</param>
    /// <param name="level">Its level.</param>
    /// <param name="associatedZaapMapId">The map of its zaap, or <see cref="NoZaap"/>.</param>
    /// <param name="basicAccountAllowed">Whether an account without a subscription may enter it.</param>
    public SubArea(int id, int nameId, int areaId, int level, long associatedZaapMapId, bool basicAccountAllowed)
    {
        Id = id;
        NameId = nameId;
        AreaId = areaId;
        Level = level;
        AssociatedZaapMapId = associatedZaapMapId;
        BasicAccountAllowed = basicAccountAllowed;
    }
}
