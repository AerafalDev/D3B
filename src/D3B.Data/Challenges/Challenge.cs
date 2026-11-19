using System.Runtime.InteropServices;

namespace D3B.Data.Challenges;

/// <summary>A challenge of the client's table, which the server names a fight's by (D461, D465).</summary>
/// <remarks>
/// <para>
/// The client's <c>ChallengesDataRoot</c> record: its texts by number, which <see cref="Texts.GameTexts"/> reads, its category,
/// its icon, the monster a dungeon's own challenge is about, and the challenges it excludes, a slice of
/// <see cref="ChallengeCatalog.Incompatibles"/>. Its completion and activation criteria are strings,
/// <see cref="ChallengeCatalog.CompletionOf"/> and <see cref="ChallengeCatalog.ActivationOf"/> (client.md).
/// </para>
/// <para>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Challenge
{
    /// <summary>Gets the identifier, as the server names a fight's challenge.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the text describing what it asks, by identifier.</summary>
    public int DescriptionId { get; }

    /// <summary>Gets the client's category: 1 for the fights' own, 9 for a dungeon boss's version of one, others for the achievements' and the dungeons'.</summary>
    public int CategoryId { get; }

    /// <summary>Gets the client's icon of it.</summary>
    public int IconId { get; }

    /// <summary>Gets the monster the challenge is about, a dungeon's boss, and 0 for none.</summary>
    public int TargetMonsterId { get; }

    /// <summary>Gets the first of the challenges it excludes in <see cref="ChallengeCatalog.Incompatibles"/>.</summary>
    public int FirstIncompatible { get; }

    /// <summary>Gets how many challenges it excludes.</summary>
    public int IncompatibleCount { get; }

    /// <summary>Initializes a new instance of the <see cref="Challenge"/> struct.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="descriptionId">The text describing it.</param>
    /// <param name="categoryId">The category.</param>
    /// <param name="iconId">The icon.</param>
    /// <param name="targetMonsterId">The monster it is about, or 0.</param>
    /// <param name="firstIncompatible">The first of the challenges it excludes.</param>
    /// <param name="incompatibleCount">How many it excludes.</param>
    public Challenge(int id, int nameId, int descriptionId, int categoryId, int iconId, int targetMonsterId, int firstIncompatible, int incompatibleCount)
    {
        Id = id;
        NameId = nameId;
        DescriptionId = descriptionId;
        CategoryId = categoryId;
        IconId = iconId;
        TargetMonsterId = targetMonsterId;
        FirstIncompatible = firstIncompatible;
        IncompatibleCount = incompatibleCount;
    }
}
