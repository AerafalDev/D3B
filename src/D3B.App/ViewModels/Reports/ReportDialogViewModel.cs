using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Reports;

namespace D3B.App.ViewModels.Reports;

/// <summary>The dialog that makes a bug report: what it is about, a message of the player's own if they wish, what goes in it, and the file with its code (D410, D411).</summary>
/// <remarks>
/// Three steps in one dialog: the form, the preview where each part can be read and unchecked, and the report written. Saving writes the file in a
/// folder, which opens, and copies its code. Nothing is sent until the player presses Send on the saved report; a report that cannot be sent stays
/// saved, to hand over on Discord with its code. Closing the dialog, by any path, stops a send still going.
/// </remarks>
internal sealed partial class ReportDialogViewModel : ObservableObject, IDisposable
{
    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Func<ReportCharacterChoice?, ReportSources?> _sources;
    private readonly Action<string> _copy;
    private readonly Action<string> _openFolder;
    private readonly Func<CancellationToken, Task<IReportSink?>>? _channel;
    private CancellationTokenSource? _sending;
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
    [NotifyPropertyChangedFor(nameof(NeedsCharacter), nameof(HasNoCharacter))]
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
            if (value.Length > MessageLimit)
            {
                // Said, and the field given its text back: a player who pastes more is told what did not go in.
                MessageNotice = _localizer.Format("ReportMessageTooLong", MessageLimit.ToString("N0", _localizer.Culture));
                OnPropertyChanged();

                return;
            }

            MessageNotice = null;

            if (SetProperty(ref _message, value))
                OnPropertyChanged(nameof(MessageCount));
        }
    }

    /// <summary>Gets what the message was refused for, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessageNotice))]
    public partial string? MessageNotice { get; private set; }

    /// <summary>Gets whether a text was refused.</summary>
    public bool HasMessageNotice => MessageNotice is not null;

    /// <summary>Gets whether no character can be chosen though the report is about the game.</summary>
    public bool HasNoCharacter => NeedsCharacter && Characters.Count is 0;

    /// <summary>Gets whether the preview has a message, which the warning about personal data goes with.</summary>
    public bool HasMessagePart => Draft?.Message is not null;

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

    /// <summary>Gets whether the report is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; private set; }

    /// <summary>Gets whether the report was sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSent { get; private set; }

    /// <summary>Gets how much of the file went, from 0 to 1.</summary>
    [ObservableProperty]
    public partial double SendProgress { get; private set; }

    /// <summary>Gets how the send went, or what to do instead, in words; null before one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSendText))]
    public partial string? SendText { get; private set; }

    /// <summary>Gets whether a send has something to say.</summary>
    public bool HasSendText => SendText is not null;

    /// <summary>Gets whether this app can send at all.</summary>
    public bool CanSend => _channel is not null;

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
    /// <param name="channel">Finds where the report goes, or null for an app that sends nothing; it gives null when no channel can be found.</param>
    public ReportDialogViewModel(Localizer localizer, Dialogs dialogs, IReadOnlyList<ReportCharacterChoice> characters, Func<ReportCharacterChoice?, ReportSources?> sources, Action<string> copy, Action<string> openFolder, ReportCharacterChoice? character = null, Func<CancellationToken, Task<IReportSink?>>? channel = null)
    {
        _channel = channel;
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

        Step = ReportStep.Preparing;

        try
        {
            // The sources are read from the host on this thread, the preparation off it: whatever either fails with, the form is given back.
            var sources = _sources(character);

            if (sources is null)
            {
                Problem = _localizer["ReportNoHost"];
                Step = ReportStep.Form;

                return;
            }

            Draft = await ReportDraft.PrepareAsync(sources, Area!.Area, Message, _localizer, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Problem = _localizer.Format("ReportNotPrepared", exception.Message);
            Step = ReportStep.Form;

            return;
        }

        OnPropertyChanged(nameof(HasMessagePart));

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
        OnPropertyChanged(nameof(HasMessagePart));
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

    [RelayCommand(CanExecute = nameof(CanSendNow))]
    private async Task SendAsync()
    {
        var written = Written!;

        IsSending = true;
        SendProgress = 0;
        SendText = null;
        _sending = new CancellationTokenSource();

        try
        {
            var sink = _channel is null ? null : await _channel(_sending.Token).ConfigureAwait(true);

            if (sink is null)
            {
                SendText = _localizer.Format("ReportNoChannel", written.Code);

                return;
            }

            var progress = new Progress<long>(sent => SendProgress = written.Size is 0 ? 1 : Math.Min(1, sent / (double)written.Size));
            var result = await sink.SendAsync(new ReportSendRequest(written.Path, written.Manifest, written.Message), progress, _sending.Token).ConfigureAwait(true);

            IsSent = result.IsSent;
            SendText = TextOf(result, written);
        }
        catch (OperationCanceledException)
        {
            SendText = _localizer.Format("ReportSendCancelled", written.Code);
        }
        catch (Exception)
        {
            // The sink answers with results; whatever else goes wrong, the file is saved and the player is told so.
            SendText = _localizer.Format("ReportSendUnreachable", written.Code);
        }
        finally
        {
            IsSending = false;
            _sending.Dispose();
            _sending = null;
        }
    }

    private bool CanSendNow()
    {
        return Written is not null && CanSend && !IsSending && !IsSent;
    }

    private string TextOf(ReportSendResult result, WrittenReport written)
    {
        return result.Status switch
        {
            ReportSendStatus.Sent => _localizer.Format("ReportSendSent", written.Code),
            ReportSendStatus.TooLarge => _localizer.Format("ReportSendTooLarge", (written.Size / 1024d / 1024d).ToString("0.#", _localizer.Culture), written.Code),
            ReportSendStatus.RateLimited => _localizer.Format("ReportSendRateLimited", Math.Ceiling(result.RetryAfter?.TotalSeconds ?? 5).ToString("0", _localizer.Culture)),
            ReportSendStatus.ChannelGone => _localizer.Format("ReportSendGone", written.Code),
            ReportSendStatus.Rejected => _localizer.Format("ReportSendRefused", written.Code),
            _ => _localizer.Format("ReportSendUnreachable", written.Code),
        };
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

    /// <summary>Stops a send still going: closing the dialog does.</summary>
    public void Dispose()
    {
        _sending?.Cancel();
    }

    [RelayCommand]
    private void Cancel()
    {
        Dispose();
        _dialogs.Close();
    }
}
