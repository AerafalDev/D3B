using D3B.Data.Icons;
using MemoryPack;

namespace D3B.Data.Hints;

/// <summary>The hints of the client's world map, their categories and their pictures: what the studio draws on its map (D414).</summary>
/// <remarks>
/// <para>
/// 835 hints of client 3.7.13.17, 6 categories used, 91 pictures: the client's own, 64 pixels square, kept as
/// <see cref="Icons"/> by the <see cref="Hint.Gfx"/> that names them (the 18 class temples' are 62 pixels in the client, set
/// in the middle of 64). Each table is sorted by identifier and found by binary search, and names its texts by number, which
/// <see cref="Texts.GameTexts"/> reads.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class HintCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "hints/catalog";

    /// <summary>Gets the hints, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Hint> Hints { get; }

    /// <summary>Gets the categories, in ascending order of identifier.</summary>
    public ReadOnlyMemory<HintCategory> Categories { get; }

    /// <summary>Gets the hints' pictures, by <see cref="Hint.Gfx"/>.</summary>
    public IconTable Icons { get; }

    /// <summary>Initializes a new instance of the <see cref="HintCatalog"/> class.</summary>
    /// <param name="hints">The hints, in strictly ascending order of identifier.</param>
    /// <param name="categories">The categories, in strictly ascending order of identifier.</param>
    /// <param name="icons">The pictures, by graphic.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public HintCatalog(ReadOnlyMemory<Hint> hints, ReadOnlyMemory<HintCategory> categories, IconTable icons)
    {
        ArgumentNullException.ThrowIfNull(icons);

        Tables.CheckAscending(hints.Span, static hint => hint.Id, nameof(hints));
        Tables.CheckAscending(categories.Span, static category => category.Id, nameof(categories));

        Hints = hints;
        Categories = categories;
        Icons = icons;
    }

    /// <summary>Finds a hint.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="hint">The hint, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetHint(int id, out Hint hint)
    {
        return Tables.TryFind(Hints.Span, id, static hint => hint.Id, out hint);
    }

    /// <summary>Finds a category.</summary>
    /// <param name="id">The category a hint names.</param>
    /// <param name="category">The category, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetCategory(int id, out HintCategory category)
    {
        return Tables.TryFind(Categories.Span, id, static category => category.Id, out category);
    }

    /// <summary>Gets the hints a world's map draws.</summary>
    /// <param name="worldMapId">The world's map.</param>
    /// <returns>Its hints, in ascending order of identifier.</returns>
    public Hint[] HintsOn(int worldMapId)
    {
        return [.. Hints.ToArray().Where(hint => hint.WorldMapId == worldMapId)];
    }
}
