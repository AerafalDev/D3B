using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using D3B.Data;
using D3B.Data.Icons;
using D3B.Data.Packs;

namespace D3B.App.ViewModels;

/// <summary>The pack's icons as bitmaps the views draw: an item's, a profession's, a monster's, a class's head and a glyph, each made once (D135, D136, D236, D323, D336).</summary>
/// <remarks>
/// One per pack, which every view model shares, made on the UI's thread as views first ask. An item the pack keeps no
/// icon for, or a pack without icons, gives none, and the view keeps its neutral square.
/// </remarks>
internal sealed class GameIcons
{
    private static readonly ConditionalWeakTable<GameTables, GameIcons> _shared = [];

    private readonly GameTables _tables;
    private readonly Dictionary<int, Bitmap?> _items = [];
    private readonly Dictionary<int, Bitmap?> _jobs = [];
    private readonly Dictionary<int, Bitmap?> _monsters = [];
    private readonly Dictionary<int, Bitmap?> _heads = [];
    private readonly Dictionary<int, Bitmap?> _glyphs = [];
    private readonly Dictionary<int, Bitmap?> _hints = [];
    private IconCatalog? _catalog;
    private IconTable? _hintIcons;
    private bool _hintsRead;
    private bool _read;

    private GameIcons(GameTables tables)
    {
        _tables = tables;
    }

    /// <summary>Gets the icons of a pack's tables.</summary>
    /// <param name="tables">The tables, or <see langword="null"/> while the host has none.</param>
    /// <returns>Their icons, or <see langword="null"/>.</returns>
    public static GameIcons? Of(GameTables? tables)
    {
        return tables is null ? null : _shared.GetValue(tables, static tables => new GameIcons(tables));
    }

    private static WriteableBitmap Draw(ReadOnlyMemory<byte> pixels, int size)
    {
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        var row = size * 4;

        using (var buffer = bitmap.Lock())
        {
            // The bitmap's rows may be wider than the icon's: each is copied at its own stride.
            for (var y = 0; y < size; y++)
                Marshal.Copy(pixels.Slice(y * row, row).ToArray(), 0, buffer.Address + (y * buffer.RowBytes), row);
        }

        return bitmap;
    }

    private static Bitmap? Cached(Dictionary<int, Bitmap?> bitmaps, IconTable table, int iconId)
    {
        if (!bitmaps.TryGetValue(iconId, out var bitmap))
            bitmaps[iconId] = bitmap = table.TryGet(iconId, out var pixels) ? Draw(pixels, table.Size) : null;

        return bitmap;
    }

    /// <summary>Gets an item's icon.</summary>
    /// <param name="itemId">The item.</param>
    /// <returns>Its icon, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Item(int itemId)
    {
        return Catalog() is { } catalog && TryGetItem(itemId, out var iconId) ? Cached(_items, catalog.Items, iconId) : null;
    }

    /// <summary>Gets a profession's icon.</summary>
    /// <param name="jobId">The profession.</param>
    /// <returns>Its icon, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Job(int jobId)
    {
        return Catalog() is { } catalog && _tables.Jobs.TryGetJob(jobId, out var job) ? Cached(_jobs, catalog.Jobs, job.IconId) : null;
    }

    /// <summary>Gets a monster's icon, its look's, which monsters share (D236).</summary>
    /// <param name="monsterId">The monster.</param>
    /// <returns>Its icon, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Monster(int monsterId)
    {
        return Catalog() is { } catalog && _tables.Monsters.TryGetMonster(monsterId, out var monster) ? Cached(_monsters, catalog.Monsters, monster.GfxId) : null;
    }

    /// <summary>Gets a class's head, which the studio draws in place of a character's initials (D323).</summary>
    /// <param name="breedId">The character's breed.</param>
    /// <param name="isFemale">Whether the character is female.</param>
    /// <returns>Its head, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Head(int breedId, bool isFemale)
    {
        return Catalog() is { } catalog ? Cached(_heads, catalog.Heads, IconCatalog.HeadOf(breedId, isFemale)) : null;
    }

    /// <summary>Gets one of the client's glyphs, white on transparent, which a view tints (D336).</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>It, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Glyph(GameGlyph glyph)
    {
        return Catalog() is { } catalog ? Cached(_glyphs, catalog.Glyphs, (int)glyph) : null;
    }

    /// <summary>Gets the client's own picture of a hint of the world map, in colour (D414).</summary>
    /// <param name="gfx">The hint's graphic.</param>
    /// <returns>It, or <see langword="null"/> when the pack keeps none of it.</returns>
    public IImage? Hint(int gfx)
    {
        return HintPictures() is { } table ? Cached(_hints, table, gfx) : null;
    }

    /// <summary>Tells whether the pack keeps a picture of a hint, without making it.</summary>
    /// <param name="gfx">The hint's graphic.</param>
    /// <returns>Whether it does.</returns>
    public bool HasHint(int gfx)
    {
        return HintPictures() is { } table && table.TryGet(gfx, out _);
    }

    private IconTable? HintPictures()
    {
        if (_hintsRead)
            return _hintIcons;

        _hintsRead = true;

        try
        {
            _hintIcons = _tables.Hints.Icons;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or PackFormatException)
        {
            // A pack without hints draws them with a plain mark.
            _hintIcons = null;
        }

        return _hintIcons;
    }

    private IconCatalog? Catalog()
    {
        if (_read)
            return _catalog;

        _read = true;

        try
        {
            _catalog = _tables.Icons;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or PackFormatException)
        {
            // A pack without icons draws the neutral squares, as before format 4.
            _catalog = null;
        }

        return _catalog;
    }

    private bool TryGetItem(int itemId, out int iconId)
    {
        iconId = 0;

        if (!_tables.Items.TryGetItem(itemId, out var item))
            return false;

        iconId = item.IconId;

        return true;
    }
}
