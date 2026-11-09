using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.Data;
using D3B.Data.Hints;
using D3B.Data.Packs;
using D3B.Data.Texts;

namespace D3B.App.ViewModels.World;

/// <summary>The points of interest the world map draws, and the ticks of the View menu that choose which (D414).</summary>
/// <remarks>
/// <para>
/// The client's own table of hints (zaaps, dungeons, workshops, banks, auction houses, class temples, transports, other places) and
/// its pictures, read from the pack. A tick for each of the client's categories, all on, and one switch for them all; the world
/// shown decides which hints there are. The names are the game's own texts in the language of the app.
/// </para>
/// <para>This is a layer of its own: the map's scene, the selection and the Outliner do not know it, and a press on a hint is a press on its map.</para>
/// </remarks>
internal sealed partial class WorldHintsViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly Dictionary<int, (WorldMapHint Hint, int CategoryId)[]> _built = [];
    private HintCatalog? _catalog;
    private GameTexts? _texts;
    private GameIcons? _icons;
    private int _world;

    /// <summary>Gets whether the pack holds hints to draw.</summary>
    public bool IsAvailable => _catalog is not null;

    /// <summary>Gets the categories, in the client's order of identifier.</summary>
    public IReadOnlyList<HintCategoryViewModel> Categories { get; private set; } = [];

    /// <summary>Gets or sets whether the map draws hints at all.</summary>
    [ObservableProperty]
    public partial bool ShowHints { get; set; } = true;

    /// <summary>Gets what the map draws: the hints of the world shown that the ticked categories hold.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WorldMapHint> Visible { get; private set; } = [];

    /// <summary>Initializes a new instance of the <see cref="WorldHintsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts, whose language names the hints.</param>
    public WorldHintsViewModel(Localizer localizer)
    {
        _localizer = localizer;
    }

    private static HintCatalog? Read(GameTables? tables)
    {
        if (tables is null)
            return null;

        try
        {
            return tables.Hints;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or PackFormatException)
        {
            // A pack without hints draws none.
            return null;
        }
    }

    /// <summary>Reads the hints of a pack, once the host runs.</summary>
    /// <param name="tables">The pack's tables, or <see langword="null"/> without them.</param>
    /// <param name="texts">The pack's texts, or <see langword="null"/>.</param>
    public void Use(GameTables? tables, GameTexts? texts)
    {
        var ticked = Categories.ToDictionary(category => category.Id, category => category.IsOn);

        _texts = texts;
        _icons = GameIcons.Of(tables);
        _catalog = Read(tables);
        _built.Clear();
        Categories = [];

        if (_catalog is { } catalog)
        {
            var used = catalog.Hints.ToArray().Select(hint => hint.CategoryId).ToHashSet();

            Categories = [.. catalog.Categories.ToArray()
                .Where(category => used.Contains(category.Id))
                .Select(category => new HintCategoryViewModel(category.Id, Say(category.NameId), ticked.GetValueOrDefault(category.Id, true), Rebuild))];
        }

        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(Categories));
        Refresh();
    }

    /// <summary>Shows the hints of another world.</summary>
    /// <param name="world">The world's id.</param>
    public void UseLayer(int world)
    {
        _world = world;
        Refresh();
    }

    /// <summary>Says the names again, in the language chosen.</summary>
    public void Relabel()
    {
        if (_catalog is not { } catalog)
            return;

        _built.Clear();

        foreach (var category in Categories)
        {
            if (catalog.TryGetCategory(category.Id, out var read))
                category.Name = Say(read.NameId);
        }

        Rebuild();
    }

    /// <summary>Says what a hint under the pointer is: its name, its category, its level if it has one and where it stands.</summary>
    /// <param name="hit">What lies under the pointer.</param>
    /// <returns>The words, or <see langword="null"/> when the hit is not a hint of the world shown.</returns>
    public string? Describe(WorldMapHit hit)
    {
        if (hit.Kind is not WorldMapHitKind.Hint || !int.TryParse(hit.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return null;

        if (BuiltOn(_world).FirstOrDefault(built => built.Hint.Id == id).Hint is not { } hint)
            return null;

        var where = _localizer.Format("Coordinates", hint.X, hint.Y);

        return hint.Level > 0
            ? _localizer.Format("WorldHudHintLevel", hint.Name, hint.Category, hint.Level, where)
            : _localizer.Format("WorldHudHint", hint.Name, hint.Category, where);
    }

    partial void OnShowHintsChanged(bool value)
    {
        Rebuild();
    }

    private void Refresh()
    {
        var on = BuiltOn(_world);

        foreach (var category in Categories)
            category.Count = on.Count(built => built.CategoryId == category.Id);

        Rebuild();
    }

    private void Rebuild()
    {
        var ticked = Categories.Where(category => category.IsOn).Select(category => category.Id).ToHashSet();

        Visible = ShowHints ? [.. BuiltOn(_world).Where(built => ticked.Contains(built.CategoryId)).Select(built => built.Hint)] : [];
    }

    private (WorldMapHint Hint, int CategoryId)[] BuiltOn(int world)
    {
        if (_catalog is not { } catalog)
            return [];

        if (!_built.TryGetValue(world, out var built))
            _built[world] = built = [.. catalog.HintsOn(world).Select(hint => (Make(catalog, hint), hint.CategoryId))];

        return built;
    }

    private WorldMapHint Make(HintCatalog catalog, Hint hint)
    {
        var category = catalog.TryGetCategory(hint.CategoryId, out var read) ? Say(read.NameId) : string.Empty;

        return new WorldMapHint(hint.Id, Say(hint.NameId), category, hint.Level, hint.X, hint.Y, HintPriority.Of(hint), _icons is { } icons && icons.HasHint(hint.Gfx) ? () => icons.Hint(hint.Gfx) : null);
    }

    private string Say(int textId)
    {
        try
        {
            if (_texts is { } texts && texts.TryGet(_localizer.GameLanguage, textId, out var text))
                return text;
        }
        catch (KeyNotFoundException)
        {
            // A pack without texts in the language names nothing.
        }

        return "#" + textId.ToString(CultureInfo.InvariantCulture);
    }
}
