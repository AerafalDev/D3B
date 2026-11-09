using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Routes;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Zones;

/// <summary>The zones: those whose editor is open, each in a tab of its own (D299, D394).</summary>
/// <remarks>
/// It follows the project as the routes do: an editor without changes takes the file as it changes, one whose asset is gone
/// closes, and one with changes keeps them. What the editors read from the host is the routes' (<see cref="RouteServices"/>): the
/// same world, maps and tiles.
/// </remarks>
internal sealed class ZonesViewModel
{
    private const int NameAttempts = 100;

    private readonly Dictionary<string, ZoneEditorViewModel> _editors = new(StringComparer.Ordinal);
    private readonly List<ZoneEditorViewModel> _open = [];
    private readonly Func<RouteServices?> _services;
    private int _version = -1;

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; }

    /// <summary>Gets what the editors read from the host, once it runs.</summary>
    public RouteServices? Services => _services();

    /// <summary>Gets the zones whose editor is open.</summary>
    public IReadOnlyList<ZoneEditorViewModel> Open => _open;

    /// <summary>Gets or sets what opens a zone's tab once its editor opens.</summary>
    public Action<ZoneEditorViewModel>? Opened { get; set; }

    /// <summary>Gets or sets what closes a zone's tab once its editor closes.</summary>
    public Action<ZoneEditorViewModel>? Closed { get; set; }

    /// <summary>Gets whether an editor holds changes not saved.</summary>
    public bool IsAnyDirty => _open.Exists(editor => editor.IsDirty);

    /// <summary>Gets whether zones can be edited: the host runs and holds the world's maps.</summary>
    public bool IsAvailable => Services is not null;

    /// <summary>Initializes a new instance of the <see cref="ZonesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    /// <param name="services">Gives what the editors read from the host, or <see langword="null"/> while it does not run.</param>
    public ZonesViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts, Func<RouteServices?> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Localizer = localizer;
        Dialogs = dialogs;
        Toasts = toasts;
        _services = services;
    }

    /// <summary>Follows the project: an editor takes its file as it changed, or closes when the file is gone.</summary>
    public void Refresh()
    {
        if (Services is not { } services || services.Library.Version == _version)
            return;

        _version = services.Library.Version;

        foreach (var editor in _open.ToList())
        {
            if (services.Library.Find<ZoneAsset>(editor.Id) is { } zone)
                editor.Update(zone);
            else
                EndEditing(editor);
        }
    }

    /// <summary>Says the open editors' words again, in the language chosen.</summary>
    public void Retell()
    {
        foreach (var editor in _open)
            editor.Relabel();
    }

    /// <summary>Finds a zone's editor, if it was opened.</summary>
    /// <param name="id">The zone's id.</param>
    /// <returns>The editor, or <see langword="null"/>.</returns>
    public ZoneEditorViewModel? Find(string id)
    {
        return _editors.GetValueOrDefault(id);
    }

    /// <summary>Opens a zone in its tab, beside the others open.</summary>
    /// <param name="id">The zone's id.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the asset is no zone.</returns>
    public ZoneEditorViewModel? OpenZone(string id)
    {
        if (Services is not { } services || services.Library.Find<ZoneAsset>(id) is not { } zone)
            return null;

        if (!_editors.TryGetValue(id, out var editor))
            _editors[id] = editor = new ZoneEditorViewModel(this, zone);

        if (!_open.Contains(editor))
            _open.Add(editor);

        Opened?.Invoke(editor);

        return editor;
    }

    /// <summary>Makes a new, empty zone in a folder of the project and opens it.</summary>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the file could not be written.</returns>
    public async Task<ZoneEditorViewModel?> CreateAsync(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (Services is not { } services)
            return null;

        try
        {
            var zone = await services.Library.SaveAsync(new ZoneAsset { Id = AssetLibrary.NewId(), Name = FreeName(services.Library) }, CancellationToken.None, folder).ConfigureAwait(true);

            _version = -1;
            Refresh();

            return OpenZone(zone.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Failed(exception);

            return null;
        }
    }

    /// <summary>Saves every zone edited that holds changes: File's Save all, Ctrl S.</summary>
    public void SaveAll()
    {
        foreach (var editor in _open.Where(editor => editor.IsDirty).ToList())
            editor.SaveCommand.Execute(null);
    }

    /// <summary>Tells that a zone was saved.</summary>
    /// <param name="editor">The editor.</param>
    public void Saved(ZoneEditorViewModel editor)
    {
        Toasts.Show(new Toast(Localizer.Format("ZoneSaved", editor.Name), Tone.Success));
    }

    /// <summary>Tells that a zone could not be saved.</summary>
    /// <param name="exception">Why.</param>
    public void Failed(Exception exception)
    {
        Toasts.Show(new Toast(Localizer.Format("SaveFailed", exception.GetBaseException().Message), Tone.Danger));
    }

    /// <summary>Asks what to do with changes not saved before a zone's tab closes.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="save">Saves the zone.</param>
    /// <param name="discard">Closes it without saving.</param>
    public void AskToLeave(ZoneEditorViewModel editor, Action save, Action discard)
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

    /// <summary>Closes a zone's editor and its tab; what it held unsaved is dropped.</summary>
    /// <param name="editor">The editor.</param>
    public void EndEditing(ZoneEditorViewModel editor)
    {
        if (!_open.Remove(editor))
            return;

        // The next time the zone opens, it reads its file again.
        _editors.Remove(editor.Id);
        Closed?.Invoke(editor);
    }

    private string FreeName(AssetLibrary library)
    {
        var name = Localizer["ZoneNewName"];

        for (var attempt = 2; library.IsTaken(AssetKind.Zone, name) && attempt <= NameAttempts; attempt++)
            name = Localizer.Format("ZoneNewNameNumbered", Localizer["ZoneNewName"], attempt);

        return name;
    }
}
