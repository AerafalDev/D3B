using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Reports;

namespace D3B.App.ViewModels.Reports;

/// <summary>The dialog that makes a bug report: what it is about, a message of the player's own if they wish, what goes in it, and the file with its code (D410, D411).</summary>
/// <remarks>
/// Three steps in one dialog: the form, the preview where each part can be read and unchecked, and the report written. Nothing is sent: the file is
/// written in a folder, which opens, and its code is copied, for the player to hand over on Discord (BR4 sends it by a webhook).
/// </remarks>
internal sealed partial class ReportDialogViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Func<ReportCharacterChoice?, ReportSources?> _sources;
    private readonly Action<string> _copy;
    private readonly Action<string> _openFolder;
    private string _message = "";

    /// <summary>Gets the areas a report may be about.</summary>
    public IReadOnlyList<ReportAreaChoice> Areas { get; }

    /// <summary>Gets the characters a report may be about, those that played this session first.</summary>
    public IReadOnlyList<ReportCharacterChoice> Characters { get; }

    /// <summary>Gets the parts of the report, once prepared.</summary>
    public ObservableCollection<ReportPartChoice> Parts { get; } = [];

    /// <summary>Gets the most characters of the message.</summary>
    public int MessageLimit { get; } = ReportContent.MessageLimit;

    /// <summary>Gets or sets what the report is about.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(NeedsCharacter))]
    public partial ReportAreaChoice? Area { get; set; }

    /// <summary>Gets or sets the character the report is about.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial ReportCharacterChoice? Character { get; set; }

    /// <summary>Gets or sets the player's own message: the field stops at the limit as it is typed, and a text past it is refused whole, never cut.</summary>
    public string Message
    {
        get => _message;
        set
        {
            if (value.Length <= MessageLimit && SetProperty(ref _message, value))
                OnPropertyChanged(nameof(MessageCount));
        }
    }

    /// <summary>Gets the step the dialog is at.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsForm), nameof(IsPreparing), nameof(IsPreview), nameof(IsDone))]
    public partial ReportStep Step { get; private set; } = ReportStep.Form;

    /// <summary>Gets why the report cannot be made or written, if it cannot.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    /// <summary>Gets whether the report can be written: a report of the game keeps its capture.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool CanSave { get; private set; }

    /// <summary>Gets the report written, once it is.</summary>
    [ObservableProperty]
    public partial WrittenReport? Written { get; private set; }

    /// <summary>Gets what the report written says: its code and what to do with it.</summary>
    [ObservableProperty]
    public partial string DoneText { get; private set; } = "";

    /// <summary>Gets the message's length against its limit.</summary>
    public string MessageCount => $"{Message.Length.ToString("N0", _localizer.Culture)} / {MessageLimit.ToString("N0", _localizer.Culture)}";

    /// <summary>Gets whether a report of this area is about a character of the game.</summary>
    public bool NeedsCharacter => Area?.Area.NeedsCapture() ?? true;

    /// <summary>Gets whether something stops the report.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Gets whether the form shows.</summary>
    public bool IsForm => Step is ReportStep.Form;

    /// <summary>Gets whether the report is being prepared.</summary>
    public bool IsPreparing => Step is ReportStep.Preparing;

    /// <summary>Gets whether the preview shows.</summary>
    public bool IsPreview => Step is ReportStep.Preview;

    /// <summary>Gets whether the report is written.</summary>
    public bool IsDone => Step is ReportStep.Done;

    internal ReportDraft? Draft { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="ReportDialogViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs, which this one closes.</param>
    /// <param name="characters">The characters a report may be about.</param>
    /// <param name="sources">Reads what a character's report is made of from the host, or null when the host does not run.</param>
    /// <param name="copy">Copies a text to the clipboard.</param>
    /// <param name="openFolder">Opens a folder.</param>
    /// <param name="character">The character to start on, if any.</param>
    public ReportDialogViewModel(Localizer localizer, Dialogs dialogs, IReadOnlyList<ReportCharacterChoice> characters, Func<ReportCharacterChoice?, ReportSources?> sources, Action<string> copy, Action<string> openFolder, ReportCharacterChoice? character = null)
    {
        _localizer = localizer;
        _dialogs = dialogs;
        _sources = sources;
        _copy = copy;
        _openFolder = openFolder;
        Characters = characters;
        Areas = [.. Enum.GetValues<ReportArea>().Select(area => new ReportAreaChoice(area, localizer["ReportArea" + area], localizer["ReportArea" + area + "Detail"]))];
        Character = character ?? (characters.Count > 0 ? characters[0] : null);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        Problem = null;

        var character = NeedsCharacter ? Character : null;
        var sources = _sources(character);

        if (sources is null)
        {
            Problem = _localizer["ReportNoHost"];

            return;
        }

        Step = ReportStep.Preparing;

        try
        {
            Draft = await ReportDraft.PrepareAsync(sources, Area!.Area, Message, _localizer, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Problem = _localizer.Format("ReportNotPrepared", exception.Message);
            Step = ReportStep.Form;

            return;
        }

        Parts.Clear();

        foreach (var part in Draft.Parts)
        {
            part.PropertyChanged += (_, _) => CanSave = Draft.CanWrite;
            Parts.Add(part);
        }

        Problem = Draft.CaptureProblem;
        CanSave = Draft.CanWrite;
        Step = ReportStep.Preview;
    }

    private bool CanGoNext()
    {
        return Area is not null && (!NeedsCharacter || Character is not null);
    }

    [RelayCommand]
    private void Back()
    {
        Draft = null;
        Problem = null;
        Parts.Clear();
        Step = ReportStep.Form;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        try
        {
            Written = Draft!.Write();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Problem = _localizer.Format("ReportNotWritten", exception.Message);

            return;
        }

        Problem = null;
        DoneText = _localizer.Format("ReportDoneLead", Written.Code);
        Step = ReportStep.Done;
        _copy(Written.Code);
        _openFolder(Path.GetDirectoryName(Written.Path)!);
    }

    [RelayCommand]
    private void CopyCode()
    {
        if (Written is { } written)
            _copy(written.Code);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Written is { } written)
            _openFolder(Path.GetDirectoryName(written.Path)!);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close();
    }
}
