using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Settings;
using D3B.Data.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Sequences;

/// <summary>The behaviors that are lists of steps: those whose editor is open, each in a tab of its own (D386).</summary>
/// <remarks>
/// It follows the project as the routes' page does: an editor without changes takes the file as it changes, one whose asset is
/// gone closes, and one with changes keeps them. Editors open once the host runs, the node catalog being its.
/// </remarks>
internal sealed class SequencesViewModel
{
    private const int NameAttempts = 100;

    private readonly Dictionary<string, SequenceEditorViewModel> _editors = new(StringComparer.Ordinal);
    private readonly List<SequenceEditorViewModel> _open = [];
    private IServiceProvider? _provider;
    private int _version = -1;

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; }

    /// <summary>Gets the project, once the host runs.</summary>
    public AssetLibrary? Library { get; private set; }

    /// <summary>Gets the kinds of nodes, whose sentences the steps are, once the host runs.</summary>
    public NodeCatalog? Nodes { get; private set; }

    /// <summary>Gets the game's values the steps name, or <see langword="null"/> without them.</summary>
    public GameReferences? Game => _provider?.GetService<GameReferences>();

    /// <summary>Gets the pack's texts, or <see langword="null"/> without them.</summary>
    public GameTexts? Texts => _provider?.GetService<GameTexts>();

    /// <summary>Gets the host's services, which the steps' fields read from.</summary>
    public IServiceProvider? Provider => _provider;

    /// <summary>Gets the lists whose editor is open.</summary>
    public IReadOnlyList<SequenceEditorViewModel> Open => _open;

    /// <summary>Gets or sets what opens a list's tab once its editor opens.</summary>
    public Action<SequenceEditorViewModel>? Opened { get; set; }

    /// <summary>Gets or sets what closes a list's tab once its editor closes.</summary>
    public Action<SequenceEditorViewModel>? Closed { get; set; }

    /// <summary>Gets whether an editor holds changes not saved.</summary>
    public bool IsAnyDirty => _open.Exists(editor => editor.IsDirty);

    /// <summary>Gets whether lists can be edited: the host runs and holds the project and the nodes.</summary>
    public bool IsAvailable => Library is not null && Nodes is not null;

    /// <summary>Initializes a new instance of the <see cref="SequencesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    public SequencesViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts)
    {
        Localizer = localizer;
        Dialogs = dialogs;
        Toasts = toasts;
    }

    /// <summary>Reads what the editors need from the host, once it runs, and follows the project.</summary>
    /// <param name="provider">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? provider)
    {
        if (provider is not null && _provider is null)
        {
            _provider = provider;
            Library = provider.GetService<AssetLibrary>();
            Nodes = provider.GetService<NodeCatalog>();
        }

        if (Library is null || Library.Version == _version)
            return;

        _version = Library.Version;

        foreach (var editor in _open.ToList())
        {
            if (Library.Find<BehaviorAsset>(editor.Id) is { } behavior)
                editor.Update(behavior);
            else
                EndEditing(editor);
        }
    }

    /// <summary>Uses a host of the caller's own, as a test does.</summary>
    /// <param name="provider">The services.</param>
    public void Use(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
        Library = provider.GetRequiredService<AssetLibrary>();
        Nodes = provider.GetRequiredService<NodeCatalog>();
        _version = Library.Version;
    }

    /// <summary>Says the open editors' words again, in the language chosen.</summary>
    public void Retell()
    {
        foreach (var editor in _open)
            editor.Relabel();
    }

    /// <summary>Finds a list's editor, if it was opened.</summary>
    /// <param name="id">The behavior's id.</param>
    /// <returns>The editor, or <see langword="null"/>.</returns>
    public SequenceEditorViewModel? Find(string id)
    {
        return _editors.GetValueOrDefault(id);
    }

    /// <summary>Tells whether a behavior opens as a list of steps.</summary>
    /// <param name="id">The behavior's id.</param>
    /// <returns>Whether the behavior is a project's own, and a list.</returns>
    public bool IsList(string id)
    {
        return Library?.Find<BehaviorAsset>(id) is { IsGiven: false } behavior && BehaviorSequences.Read(behavior) is not null;
    }

    /// <summary>Opens a behavior in its tab, beside the others open.</summary>
    /// <param name="id">The behavior's id.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the behavior is none of the project's that is a list of steps.</returns>
    public SequenceEditorViewModel? OpenList(string id)
    {
        if (Library?.Find<BehaviorAsset>(id) is not { } behavior)
            return null;

        if (behavior.IsGiven || BehaviorSequences.Read(behavior) is null)
        {
            // A behavior with states, rules or branches is the canvas's, which comes with 7e (D306).
            Toasts.Show(new Toast(Localizer["SequenceNotAList"], Tone.Info));

            return null;
        }

        if (!_editors.TryGetValue(id, out var editor))
            _editors[id] = editor = new SequenceEditorViewModel(this, behavior);

        if (!_open.Contains(editor))
            _open.Add(editor);

        Opened?.Invoke(editor);

        return editor;
    }

    /// <summary>Makes a new, empty list in a folder of the project, without opening it.</summary>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <param name="name">Its name, or <see langword="null"/> for one the project has not.</param>
    /// <returns>The behavior saved, or <see langword="null"/> when the host does not run or the file could not be written.</returns>
    public async Task<BehaviorAsset?> MakeAsync(string folder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (Library is not { } library)
            return null;

        try
        {
            var made = await library.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = name ?? FreeName(library) }, BehaviorSequence.Empty), CancellationToken.None, folder).ConfigureAwait(true);

            _version = -1;
            Refresh(null);

            return made;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Failed(exception);

            return null;
        }
    }

    /// <summary>Makes a new, empty list in a folder of the project and opens it.</summary>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <returns>The editor, or <see langword="null"/> when the host does not run or the file could not be written.</returns>
    public async Task<SequenceEditorViewModel?> CreateAsync(string folder)
    {
        return await MakeAsync(folder).ConfigureAwait(true) is { } behavior ? OpenList(behavior.Id) : null;
    }

    /// <summary>Saves every list edited that holds changes: File's Save all, Ctrl S.</summary>
    public void SaveAll()
    {
        foreach (var editor in _open.Where(editor => editor.IsDirty).ToList())
            editor.SaveCommand.Execute(null);
    }

    /// <summary>Tells that a list was saved.</summary>
    /// <param name="editor">The editor.</param>
    public void Saved(SequenceEditorViewModel editor)
    {
        Toasts.Show(new Toast(Localizer.Format("SequenceSaved", editor.Name), Tone.Success));
    }

    /// <summary>Tells that a list could not be saved.</summary>
    /// <param name="exception">Why.</param>
    public void Failed(Exception exception)
    {
        Toasts.Show(new Toast(Localizer.Format("SaveFailed", exception.GetBaseException().Message), Tone.Danger));
    }

    /// <summary>Asks what to do with changes not saved before a list's tab closes.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="save">Saves the list.</param>
    /// <param name="discard">Closes it without saving.</param>
    public void AskToLeave(SequenceEditorViewModel editor, Action save, Action discard)
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

    /// <summary>Closes a list's editor and its tab; what it held unsaved is dropped.</summary>
    /// <param name="editor">The editor.</param>
    public void EndEditing(SequenceEditorViewModel editor)
    {
        if (!_open.Remove(editor))
            return;

        // The next time the list opens, it reads its file again.
        _editors.Remove(editor.Id);
        Closed?.Invoke(editor);
    }

    private string FreeName(AssetLibrary library)
    {
        var name = Localizer["SequenceNewName"];

        for (var attempt = 2; library.IsTaken(AssetKind.Behavior, name) && attempt <= NameAttempts; attempt++)
            name = Localizer.Format("SequenceNewNameNumbered", Localizer["SequenceNewName"], attempt);

        return name;
    }
}
