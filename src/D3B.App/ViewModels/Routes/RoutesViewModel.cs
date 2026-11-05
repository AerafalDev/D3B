using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Movement;
using D3B.Bot.Routes;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Data.WorldMaps;
using D3B.Engine.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Routes;

/// <summary>The routes: those whose editor is open, each in a tab of its own, and what the editors read from the host (D299, D371).</summary>
/// <remarks>
/// It follows the project as the profiles do (D330): an editor without changes takes the file as it changes, one whose asset
/// is gone closes, and one with changes keeps them. Editors open once the host runs, the world's path and maps being its.
/// </remarks>
internal sealed class RoutesViewModel
{
    private const int NameAttempts = 100;

    private readonly Dictionary<string, RouteEditorViewModel> _editors = new(StringComparer.Ordinal);
    private readonly List<RouteEditorViewModel> _open = [];
    private IServiceProvider? _provider;
    private RouteServices? _services;
    private int _version = -1;

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; }

    /// <summary>Gets what the editors read from the host, once it runs: made when first asked for, the world's path being a search structure to build.</summary>
    public RouteServices? Services
    {
        get
        {
            if (_services is null && _provider is not null)
                _services = ServicesOf(_provider);

            return _services;
        }
    }

    /// <summary>Gets the routes whose editor is open.</summary>
    public IReadOnlyList<RouteEditorViewModel> Open => _open;

    /// <summary>Gets or sets what opens a route's tab once its editor opens.</summary>
    public Action<RouteEditorViewModel>? Opened { get; set; }

    /// <summary>Gets or sets what closes a route's tab once its editor closes.</summary>
    public Action<RouteEditorViewModel>? Closed { get; set; }

    /// <summary>Gets whether an editor holds changes not saved.</summary>
    public bool IsAnyDirty => _open.Exists(editor => editor.IsDirty);

    /// <summary>Gets whether routes can be edited: the host runs and holds the world's maps.</summary>
    public bool IsAvailable => _services is not null || _provider is not null;

    /// <summary>Initializes a new instance of the <see cref="RoutesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    public RoutesViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts)
    {
        Localizer = localizer;
        Dialogs = dialogs;
        Toasts = toasts;
    }

    /// <summary>Reads what the editors need from the host, once it runs, and follows the project.</summary>
    /// <param name="provider">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? provider)
    {
        _provider ??= provider;

        if (_services is null || _services.Library.Version == _version)
            return;

        _version = _services.Library.Version;

        foreach (var editor in _open.ToList())
        {
            if (_services.Library.Find<RouteAsset>(editor.Id) is { } route)
                editor.Update(route);
            else
                EndEditing(editor);
        }
    }

    /// <summary>Uses services of the caller's own, as a test does.</summary>
    /// <param name="services">The services.</param>
    public void Use(RouteServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        _version = services.Library.Version;
    }

    /// <summary>Says the open editors' words again, in the language chosen.</summary>
    public void Retell()
    {
        foreach (var editor in _open)
            editor.Relabel();
    }

    /// <summary>Finds a route's editor, if it was opened.</summary>
    /// <param name="id">The route's id.</param>
    /// <returns>The editor, or <see langword="null"/>.</returns>
    public RouteEditorViewModel? Find(string id)
    {
        return _editors.GetValueOrDefault(id);
    }

    /// <summary>Opens a route in its tab, beside the others open.</summary>
    /// <param name="id">The route's id.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the asset is no route.</returns>
    public RouteEditorViewModel? OpenRoute(string id)
    {
        if (Services is not { } services || services.Library.Find<RouteAsset>(id) is not { } route)
            return null;


        if (!_editors.TryGetValue(id, out var editor))
            _editors[id] = editor = new RouteEditorViewModel(this, route);

        if (!_open.Contains(editor))
            _open.Add(editor);

        Opened?.Invoke(editor);

        return editor;
    }

    /// <summary>Makes a new, empty route in a folder of the project and opens it.</summary>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the file could not be written.</returns>
    public async Task<RouteEditorViewModel?> CreateAsync(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (Services is not { } services)
            return null;

        try
        {
            var route = await services.Library.SaveAsync(new RouteAsset { Id = AssetLibrary.NewId(), Name = FreeName(services.Library) }, CancellationToken.None, folder).ConfigureAwait(true);

            _version = -1;
            Refresh(null);

            return OpenRoute(route.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Failed(exception);

            return null;
        }
    }

    /// <summary>Saves every route edited that holds changes: File's Save all, Ctrl S.</summary>
    public void SaveAll()
    {
        foreach (var editor in _open.Where(editor => editor.IsDirty).ToList())
            editor.SaveCommand.Execute(null);
    }

    /// <summary>Tells that a route was saved.</summary>
    /// <param name="editor">The editor.</param>
    public void Saved(RouteEditorViewModel editor)
    {
        Toasts.Show(new Toast(Localizer.Format("RouteSaved", editor.Name), Tone.Success));
    }

    /// <summary>Tells that a route could not be saved.</summary>
    /// <param name="exception">Why.</param>
    public void Failed(Exception exception)
    {
        Toasts.Show(new Toast(Localizer.Format("SaveFailed", exception.GetBaseException().Message), Tone.Danger));
    }

    /// <summary>Asks what to do with changes not saved before a route's tab closes.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="save">Saves the route.</param>
    /// <param name="discard">Closes it without saving.</param>
    public void AskToLeave(RouteEditorViewModel editor, Action save, Action discard)
    {
        Dialogs.Ask(new QuestionViewModel(
            Dialogs,
            Localizer["LeaveUnsavedTitle"],
            Localizer.Format("LeaveUnsavedDetail", editor.Name),
            Localizer["Save"],
            Localizer["Stay"],
            save,
            otherText: Localizer["Discard"],
            other: discard));
    }

    /// <summary>Closes a route's editor and its tab; what it held unsaved is dropped.</summary>
    /// <param name="editor">The editor.</param>
    public void EndEditing(RouteEditorViewModel editor)
    {
        if (!_open.Remove(editor))
            return;

        // The next time the route opens, it reads its file again.
        _editors.Remove(editor.Id);
        Closed?.Invoke(editor);
    }

    private static RouteServices? ServicesOf(IServiceProvider provider)
    {
        var library = provider.GetService<AssetLibrary>();
        var world = provider.GetService<GameWorld>();
        var tables = provider.GetService<GameTables>();
        var index = provider.GetService<WorldMapIndex>();
        var tiles = provider.GetService<WorldMapTiles>();

        if (library is null || world is null || tables is null || index is null || tiles is null)
            return null;

        try
        {
            return new RouteServices(library, new RoutePlanner(new WorldPathfinder(world.Graph, world.Atlas, NpcWays.Measured)), index, tables.WorldMaps, world.Atlas, provider.GetService<GameTexts>(), tiles.Read, provider.GetService<NodeCatalog>(), provider.GetService<GameReferences>(), provider.GetService<GameMaps>(), provider);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidDataException or IOException)
        {
            // A pack without the world's maps, such as one of an older format: no route is edited on it.
            return null;
        }
    }

    private string FreeName(AssetLibrary library)
    {
        var name = Localizer["RouteNewName"];

        for (var attempt = 2; library.IsTaken(AssetKind.Route, name) && attempt <= NameAttempts; attempt++)
            name = Localizer.Format("RouteNewNameNumbered", Localizer["RouteNewName"], attempt);

        return name;
    }
}
