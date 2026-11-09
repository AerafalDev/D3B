using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.ViewModels.Settings;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Activities;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>A profile, edited in a tab of its own: its name, the behavior it is a variant of, its settings and what they change (D300, D330).</summary>
/// <remarks>A profile given with the app is read only: copied to be changed (D296).</remarks>
internal sealed partial class ProfileViewModel : ObservableObject, IUndoable
{
    private readonly ProfilesViewModel _page;
    private ProfileAsset? _profile;
    private bool _loading;
    private (string Name, string? Activity, string? Settings) _baseline;
    private UndoHistory<ProfileDraft> _history = new(new ProfileDraft(string.Empty, null, null), SameDraft);
    private string? _defaults;
    private string? _behaviorId;

    /// <summary>Gets what keys the profile's tab, the same once a new one is saved.</summary>
    public string TabKey { get; }

    /// <summary>Gets the folder a new profile is saved in, relative to the project's; empty for its root.</summary>
    public string Folder { get; init; } = "";

    /// <summary>Gets the profile's id, empty until a new one is saved.</summary>
    public string Id => _profile?.Id ?? "";

    /// <summary>Gets whether the profile is a new one, not saved yet.</summary>
    public bool IsNew => _profile is null;

    /// <summary>Gets whether the profile was given with the app: read only, copied to be changed.</summary>
    public bool IsGiven => _profile?.IsGiven == true;

    /// <summary>Gets whether the profile may be edited or deleted: one of the project's own.</summary>
    public bool IsOwn => !IsGiven;

    /// <summary>Gets whether the profile is shown but not changed: given with the app, it is copied to be changed (D296).</summary>
    public bool IsReadOnly => IsGiven;

    /// <inheritdoc/>
    public bool CanUndo => IsEditing && _history.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo => IsEditing && _history.CanRedo;

    /// <summary>Gets the world's map beside the form, where the place is picked (D415).</summary>
    public ProfileMapViewModel Map { get; }

    /// <summary>Gets the name of the behavior the profile is a variant of, as its header says it (D330).</summary>
    [ObservableProperty]
    public partial string BehaviorName { get; private set; } = "";

    /// <summary>Gets how many values the profile changes from its behavior, in words.</summary>
    [ObservableProperty]
    public partial string ChangedText { get; private set; } = "";

    /// <summary>Gets whether the profile changes a value of its behavior.</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; private set; }

    /// <summary>Gets the activities a profile may run, named in the app's language.</summary>
    public IReadOnlyList<ActivityChoiceViewModel> Activities => _page.Activities;

    /// <summary>Gets the id of the behavior the profile plays when that is not one activity whole, such as a list of steps; <see langword="null"/> otherwise (D386).</summary>
    public string? BehaviorId => _behaviorId;

    /// <summary>Gets whether the profile plays a behavior of the project that is not one activity whole, such as a list of steps (D386).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityMissing))]
    public partial bool IsBehaviorProfile { get; private set; }

    /// <summary>Gets what the profile's behavior is made of, in words: its steps, and whether it starts again.</summary>
    [ObservableProperty]
    public partial string BehaviorDetail { get; private set; } = "";

    /// <summary>Gets whether the behavior is a list of steps, whose editor the profile opens.</summary>
    [ObservableProperty]
    public partial bool CanEditSteps { get; private set; }

    /// <summary>Gets the activity the profile's behavior plays whole, if it still does.</summary>
    public ActivityChoiceViewModel? SavedActivity => _page.FormOf(_profile).Activity is { } saved ? Activities.FirstOrDefault(activity => activity.Definition?.Id == saved.Id) : null;

    /// <summary>Gets the settings the profile was saved with, its behavior's where it changes none, if it was saved.</summary>
    public string? SavedSettings => _page.FormOf(_profile).Settings;

    /// <summary>Gets the profile's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets what its header says: its name, or the name being typed for a new one, else that it is new.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>Gets the name of the activity it runs.</summary>
    [ObservableProperty]
    public partial string ActivityName { get; private set; } = "";

    /// <summary>Gets why the profile is to be looked at, if it is: its activity gone, or a setting that no longer reads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsReview))]
    public partial string? Review { get; private set; }

    /// <summary>Gets whether the profile shows its editor.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    /// <summary>Gets whether what is edited differs from what was saved, or the profile was never saved.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets or sets the name being edited.</summary>
    [ObservableProperty]
    public partial string EditName { get; set; } = "";

    /// <summary>Gets what is wrong with the name being edited, if anything.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameProblem))]
    public partial string? NameProblem { get; private set; }

    /// <summary>Gets or sets the activity being edited: chosen again only when the profile's is gone, its settings from their defaults.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityMissing))]
    public partial ActivityChoiceViewModel? EditActivity { get; set; }

    /// <summary>Gets the editor of the activity's settings.</summary>
    [ObservableProperty]
    public partial SettingsEditor? Editor { get; private set; }

    /// <summary>Gets why the profile was not saved or deleted, if it was not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Gets whether the profile can be saved: a name of its own, an activity, settings that hold.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool CanSave { get; private set; }

    /// <summary>Gets whether the profile is to be looked at.</summary>
    public bool NeedsReview => Review is not null;

    /// <summary>Gets whether something is wrong with the name being edited.</summary>
    public bool HasNameProblem => NameProblem is not null;

    /// <summary>Gets whether the profile was not saved or deleted.</summary>
    public bool HasError => Error is not null;

    /// <summary>Gets whether the activity edited is none the modules registered, as when the profile's is gone: it is chosen again.</summary>
    public bool IsActivityMissing => !IsBehaviorProfile && EditActivity?.Definition is null;

    /// <summary>Initializes a new instance of the <see cref="ProfileViewModel"/> class.</summary>
    /// <param name="page">The profiles' page.</param>
    /// <param name="profile">The profile, or <see langword="null"/> for a new one.</param>
    public ProfileViewModel(ProfilesViewModel page, ProfileAsset? profile)
    {
        _page = page;
        _profile = profile;
        TabKey = profile?.Id ?? Guid.NewGuid().ToString("N");
        Map = new ProfileMapViewModel(page.Localizer, () => page.WorldServices(), zone => page.EditZone?.Invoke(zone));
        Show();
    }

    /// <summary>Shows the profile as it was saved, above its editor if it is being edited.</summary>
    /// <param name="profile">The profile, as saved.</param>
    public void Update(ProfileAsset profile)
    {
        _profile = profile;
        Show();
    }

    /// <summary>Shows the profile again, in the app's language, and its characters; its editor keeps what is being edited.</summary>
    public void Relabel()
    {
        Show();

        // The activities are named again in the new language: the one being edited is chosen among them, its settings kept,
        // whatever the list clears as its items change.
        var edited = EditActivity?.Definition;

        _loading = true;

        try
        {
            OnPropertyChanged(nameof(Activities));

            if (edited is not null)
                EditActivity = Activities.FirstOrDefault(activity => activity.Definition?.Id == edited.Id) ?? EditActivity;
        }
        finally
        {
            _loading = false;
        }

        UpdateCanSave();

        Editor?.Relabel();
        Map.Relabel();
        CheckName();
    }

    /// <summary>Closes the editor, its changes dropped.</summary>
    public void Cancel()
    {
        IsEditing = false;
        IsDirty = false;
        Error = null;
        Editor = null;
        Map.Use(null);
        _page.EndEditing(this);

        if (!IsNew)
            Show();
    }

    /// <summary>Opens the editor in its tab, from the profile as saved; a given one is shown, not changed.</summary>
    [RelayCommand]
    public void Edit()
    {
        if (IsEditing)
        {
            _page.BeginEditing(this);

            return;
        }

        Open(_profile?.Name ?? "", IsBehaviorProfile ? null : SavedActivity ?? (IsNew && Activities.Count > 0 ? Activities[0] : null), SavedSettings);
    }

    /// <summary>Opens the editor of a profile never saved, its name and activity chosen.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="activity">Its activity.</param>
    /// <param name="settings">The settings it starts from, or <see langword="null"/> for the activity's defaults.</param>
    public void EditNew(string name, ActivityChoiceViewModel activity, string? settings)
    {
        Open(name, activity, settings);
    }

    /// <summary>Opens the editor of a profile never saved that plays a behavior of the project, its name chosen.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="behaviorId">The behavior's id.</param>
    public void EditNewBehavior(string name, string behaviorId)
    {
        _behaviorId = behaviorId;
        ShowBehavior();
        Open(name, null, null);
    }

    private void Open(string name, ActivityChoiceViewModel? activity, string? settings)
    {
        _loading = true;

        try
        {
            EditName = name;
            EditActivity = activity;
            Load(settings);
        }
        finally
        {
            _loading = false;
        }

        IsEditing = true;
        _baseline = (EditName.Trim(), EditActivity?.Definition?.Id, Editor?.Write());
        _history = new UndoHistory<ProfileDraft>(Draft(), SameDraft);
        Moved();
        CheckName();
        CheckDirty();
        _page.BeginEditing(this);
    }

    private void Show()
    {
        var culture = _page.Localizer.Culture;
        var (definition, settings) = _page.FormOf(_profile);

        if (_profile is not null)
            _behaviorId = definition is null && _page.Assets?.Find<BehaviorAsset>(_profile.Behavior) is { } behavior ? behavior.Id : null;

        ShowBehavior();

        // Computed from the profile kept, which saving a new one gives: the view hears it is new no more.
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsGiven));
        OnPropertyChanged(nameof(IsOwn));
        EditCommand.NotifyCanExecuteChanged();
        AskDeleteCommand.NotifyCanExecuteChanged();
        Name = _profile is null ? "" : _profile.Title?.In(culture) ?? _profile.Name;
        RefreshTitle();
        ActivityName = IsBehaviorProfile ? BehaviorName : definition?.Name.In(culture) ?? _profile?.Behavior ?? "";

        Review = _profile is null || IsBehaviorProfile ? null
            : definition is null ? _page.Localizer.Format("BehaviorMissing", _profile.Behavior)
            : definition.Settings.Read(settings).Reset.Count > 0 ? _page.Localizer["ProfileNeedsReview"]
            : null;
    }

    private void ShowBehavior()
    {
        IsBehaviorProfile = _behaviorId is not null;

        if (_behaviorId is null)
        {
            CanEditSteps = false;
            BehaviorDetail = "";

            return;
        }

        var behavior = _page.Assets?.Find<BehaviorAsset>(_behaviorId);
        var sequence = behavior is null ? null : BehaviorSequences.Read(behavior);
        var localizer = _page.Localizer;

        CanEditSteps = sequence is not null && behavior is { IsGiven: false };
        EditStepsCommand.NotifyCanExecuteChanged();

        if (behavior is null)
        {
            BehaviorName = "";
            BehaviorDetail = localizer.Format("BehaviorMissing", _behaviorId);
            ChangedText = "";
            HasChanges = false;

            return;
        }

        BehaviorName = behavior.Title?.In(localizer.Culture) ?? behavior.Name;
        BehaviorDetail = sequence is null ? localizer["BehaviorNotAList"]
            : sequence.Steps.IsEmpty ? localizer["SequenceEmptyDetail"]
            : localizer.Plural("SequenceStepsDetail", sequence.Steps.Length) + (sequence.Repeats ? " · " + localizer["SequenceRepeatsDetail"] : "");
        ChangedText = "";
        HasChanges = false;
    }

    [RelayCommand(CanExecute = nameof(CanEditSteps))]
    private void EditSteps()
    {
        if (_behaviorId is not null)
            _page.OpenSequence(_behaviorId);
    }

    private void Load(string? settings)
    {
        // The editor is made again at each value given back, undo or activity: the folds the hand opened stay as they were.
        var opened = Editor?.Sections.Select(section => section.IsAdvancedOpen).ToList() ?? [];

        if (Editor is { } previous)
        {
            previous.PropertyChanged -= OnEditorChanged;
            previous.Edited -= OnEdited;
        }

        if (EditActivity?.Definition is not { } definition)
        {
            Editor = null;
            Map.Use(null);
            UpdateCanSave();

            return;
        }

        var reading = definition.Settings.Read(settings);
        var behavior = BehaviorOf(definition);

        _defaults = behavior is null ? null : ProfileForms.SettingsOf(behavior, null);
        BehaviorName = behavior is null ? "" : behavior.Title?.In(_page.Localizer.Culture) ?? behavior.Name;
        Editor = new SettingsEditor(definition.Settings, reading.Settings, _page.Game, _page.Names, _page.Localizer, _page.Services, IsNew ? [] : _page.LevelsOf(Id), _page.Dialogs, _defaults is null ? null : definition.Settings.Read(_defaults).Settings)
        {
            Resets = ResetField,
        };
        Editor.PropertyChanged += OnEditorChanged;
        Editor.Edited += OnEdited;

        for (var index = 0; index < Math.Min(opened.Count, Editor.Sections.Count); index++)
            Editor.Sections[index].IsAdvancedOpen = opened[index];

        Map.Use(Editor);

        UpdateCanSave();
        ShowChanges();
    }

    private BehaviorAsset? BehaviorOf(ActivityDefinition definition)
    {
        // The profile keeps its behavior while it plays the activity edited; one whose behavior is gone takes the activity's given one.
        return _profile is { } saved && _page.FormOf(saved).Activity?.Id == definition.Id ? _page.Assets?.Find<BehaviorAsset>(saved.Behavior) : _page.BehaviorOf(definition);
    }

    private void ShowChanges()
    {
        var count = Editor?.ChangedCount ?? 0;

        HasChanges = count > 0;
        ChangedText = count > 0 ? _page.Localizer.Plural("ValuesChanged", count) : _page.Localizer["NoValueChanged"];
    }

    private void ResetField(SettingFieldViewModel field)
    {
        if (IsReadOnly || Editor is not { Draft: { } draft } editor || _defaults is null || EditActivity?.Definition is not { } definition)
            return;

        var defaults = definition.Settings.Read(_defaults).Settings!;

        // The editor is made again from the value given back: each field then shows what it reads, the others kept.
        field.Definition.SetValue(draft, field.Definition.GetValue(defaults));
        Load(editor.Write());
        CheckDirty();
        Record();
    }

    /// <summary>Gives every value the behavior's back: "Tout remettre" (D330).</summary>
    [RelayCommand]
    public void ResetAll()
    {
        if (IsReadOnly || _defaults is null)
            return;

        Load(_defaults);
        CheckDirty();
        Record();
    }

    /// <summary>Copies the profile, given with the app, into the project, and opens the copy.</summary>
    /// <returns>A task that completes once copied.</returns>
    [RelayCommand]
    public Task CopyToProjectAsync()
    {
        return _page.CopyToProjectAsync(this);
    }

    private void OnEdited(object? sender, EventArgs e)
    {
        CheckDirty();
        ShowChanges();
        Record(Editor?.TypingRun);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (IsEditing && _history.TryUndo(out var state))
            Restore(state);
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (IsEditing && _history.TryRedo(out var state))
            Restore(state);
    }

    private ProfileDraft Draft()
    {
        return new ProfileDraft(EditName, EditActivity?.Definition?.Id, Editor?.Write());
    }

    private void Record(string? run = null)
    {
        if (IsEditing && !_loading && _history.Record(Draft(), run))
            Moved();
    }

    private void Restore(ProfileDraft state)
    {
        _loading = true;

        try
        {
            EditName = state.Name;
            EditActivity = Activities.FirstOrDefault(activity => activity.Definition?.Id == state.Activity) ?? EditActivity;
            Load(state.Settings);
        }
        finally
        {
            _loading = false;
        }

        CheckName();
        CheckDirty();
        ShowChanges();
        Moved();
    }

    private void Moved()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private static bool SameDraft(ProfileDraft first, ProfileDraft second)
    {
        return first == second;
    }

    private void CheckDirty()
    {
        IsDirty = IsEditing && (IsNew || EditName.Trim() != _baseline.Name || EditActivity?.Definition?.Id != _baseline.Activity || Editor?.Write() != _baseline.Settings);
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsEditor.HasProblems))
            UpdateCanSave();
    }

    private void CheckName()
    {
        if (!IsEditing)
        {
            NameProblem = null;

            return;
        }

        NameProblem = string.IsNullOrWhiteSpace(EditName) ? _page.Localizer["NameRequired"]
            : AssetLibrary.NameProblem(EditName) is not null ? _page.Localizer["NameInvalid"]
            : _page.IsTaken(EditName, IsNew ? null : Id) ? _page.Localizer["NameTaken"]
            : null;
        UpdateCanSave();
    }

    private void UpdateCanSave()
    {
        CanSave = IsEditing && !IsReadOnly && NameProblem is null
            && (IsBehaviorProfile ? _behaviorId is not null && _page.Assets?.Find<BehaviorAsset>(_behaviorId) is not null : EditActivity?.Definition is not null && Editor is { HasProblems: false });
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (IsBehaviorProfile)
        {
            await SaveBehaviorProfileAsync().ConfigureAwait(true);

            return;
        }

        if (_page.Assets is not { } assets || EditActivity?.Definition is not { } definition || Editor is not { } editor)
            return;

        var behavior = BehaviorOf(definition);

        if (behavior is null)
        {
            Error = _page.Localizer.Format("ProfileBehaviorMissing", Title);

            return;
        }

        try
        {
            _profile = await assets.SaveAsync(
                new ProfileAsset
                {
                    Id = _profile?.Id ?? AssetLibrary.NewId(),
                    Name = EditName.Trim(),
                    Path = _profile?.Path,
                    Behavior = behavior.Id,
                    Values = ProfileForms.ValuesOf(behavior, editor.Write()),
                },
                CancellationToken.None,
                Folder);
            // The tab stays open on what was saved, as an editor's does.
            _baseline = (EditName.Trim(), EditActivity?.Definition?.Id, editor.Write());
            IsDirty = false;
            Error = null;
            Show();
            CheckName();
            _page.Saved(this);
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Error = _page.Localizer.Format("SaveFailed", exception.GetBaseException().Message);
        }
    }

    private async Task SaveBehaviorProfileAsync()
    {
        if (_page.Assets is not { } assets || _behaviorId is null)
            return;

        try
        {
            // What the profile changes of its behavior stays as it is: the list of steps has no public setting to change (D386).
            var profile = new ProfileAsset { Id = _profile?.Id ?? AssetLibrary.NewId(), Name = EditName.Trim(), Path = _profile?.Path, Behavior = _behaviorId };

            _profile = await assets.SaveAsync(_profile is null ? profile : profile with { Values = _profile.Values }, CancellationToken.None, Folder);
            _baseline = (EditName.Trim(), null, null);
            IsDirty = false;
            Error = null;
            Show();
            CheckName();
            _page.Saved(this);
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Error = _page.Localizer.Format("SaveFailed", exception.GetBaseException().Message);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsDirty)
        {
            Cancel();

            return;
        }

        var localizer = _page.Localizer;

        _page.Dialogs.Ask(new QuestionViewModel(
            _page.Dialogs,
            localizer["LeaveUnsavedTitle"],
            localizer.Format("LeaveUnsavedDetail", Title),
            localizer["Save"],
            localizer["Stay"],
            () => SaveCommand.Execute(null),
            otherText: localizer["Discard"],
            other: Cancel));
    }

    [RelayCommand]
    private void Duplicate()
    {
        _page.Duplicate(this);
    }

    [RelayCommand(CanExecute = nameof(IsOwn))]
    private void AskDelete()
    {
        var localizer = _page.Localizer;
        var players = IsNew ? 0 : _page.PlayersOf(Id).Count;

        _page.Dialogs.Ask(new QuestionViewModel(
            _page.Dialogs,
            localizer.Format("DeleteProfileTitle", Title),
            players == 0 ? null : localizer.Plural("DeleteProfilePlayers", players),
            localizer["Delete"],
            localizer["Cancel"],
            () => DeleteCommand.Execute(null),
            destructive: true));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew)
        {
            Cancel();

            return;
        }

        if (_page.Assets is not { } assets)
            return;

        try
        {
            await assets.DeleteAsync(Id, CancellationToken.None);

            // The characters that played it are left without one, as a deleted profile always left them (D99).
            if (_page.Services?.GetService<AccountBook>() is { } accounts)
                await accounts.LeaveProfileAsync(Id, CancellationToken.None);

            _page.Deleted(this);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.EntityFrameworkCore.DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            Error = _page.Localizer.Format("SaveFailed", exception.GetBaseException().Message);
        }
    }

    private void RefreshTitle()
    {
        Title = !IsNew ? Name : string.IsNullOrWhiteSpace(EditName) ? _page.Localizer["NewProfile"] : EditName.Trim();
    }

    partial void OnEditNameChanged(string value)
    {
        RefreshTitle();
        CheckName();
        CheckDirty();
        Record("name");
    }

    partial void OnEditActivityChanged(ActivityChoiceViewModel? value)
    {
        // Another activity has other settings: they start from its defaults.
        if (!_loading)
        {
            Load(null);
            CheckDirty();
            Record();
        }
    }

    partial void OnIsEditingChanged(bool value)
    {
        UpdateCanSave();
        Moved();
    }

    private readonly record struct ProfileDraft(string Name, string? Activity, string? Settings);
}
