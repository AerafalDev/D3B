using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Studio;

/// <summary>The title row's run block: the chosen character, what it does live, and only the buttons its state allows (D322).</summary>
/// <remarks>
/// <para>
/// None chosen, play shows disabled; stopped, play; playing, pause and stop; paused, play and stop. Play starts the activity
/// the character's card would, its own profile first, or resumes it once paused; pause cuts what the bot does now, as the
/// player's hand does, and stop ends the activity. A fight counts as playing.
/// </para>
/// <para>
/// The character is kept by its key: it stays chosen while its bot is made again, disconnected, then connected again.
/// </para>
/// </remarks>
internal sealed partial class RunBlockViewModel : ObservableObject
{
    private readonly BotsViewModel _bots;
    private readonly Localizer _localizer;
    private CharacterKey? _chosen;

    /// <summary>Gets the characters connected, which the block's menu offers.</summary>
    public ObservableCollection<BotViewModel> Online { get; } = [];

    /// <summary>Gets the character chosen, while its bot exists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChosen))]
    public partial BotViewModel? Chosen { get; private set; }

    /// <summary>Gets where the chosen character stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlay), nameof(ShowsPause), nameof(ShowsStop), nameof(IsPlaying), nameof(IsPaused))]
    public partial RunState State { get; private set; }

    /// <summary>Gets the chosen character's name, or what to do when none is.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets what the chosen character does, live.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    /// <summary>Gets the chosen character's initials, its portrait without its class's head.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets the chosen character's class's head (D323).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial IImage? Head { get; private set; }

    /// <summary>Gets the chosen character's portrait's colour, its breed's.</summary>
    [ObservableProperty]
    public partial IBrush? PortraitColor { get; private set; }

    /// <summary>Gets the key of the character chosen, online or not.</summary>
    public CharacterKey? ChosenKey => _chosen;

    /// <summary>Gets or sets the characters' panel, which names a character chosen offline.</summary>
    public CharactersViewModel? Characters { get; set; }

    /// <summary>Gets whether the block shows the chosen character's head.</summary>
    public bool HasHead => Head is not null;

    /// <summary>Gets whether play does something now.</summary>
    [ObservableProperty]
    public partial bool CanPlay { get; private set; }

    /// <summary>Gets why play does nothing, or what it does.</summary>
    [ObservableProperty]
    public partial string PlayTip { get; private set; } = "";

    /// <summary>Gets whether no character is connected, which the menu says.</summary>
    [ObservableProperty]
    public partial bool IsNobodyOnline { get; private set; } = true;

    /// <summary>Gets or sets what else follows the character chosen: the panel's cards and the inspector.</summary>
    public Action<CharacterKey?>? Chose { get; set; }

    /// <summary>Gets whether a character is chosen.</summary>
    public bool IsChosen => Chosen is not null;

    /// <summary>Gets whether play shows.</summary>
    public bool ShowsPlay => State is RunState.None or RunState.Stopped or RunState.Paused;

    /// <summary>Gets whether pause shows.</summary>
    public bool ShowsPause => State is RunState.Playing;

    /// <summary>Gets whether stop shows.</summary>
    public bool ShowsStop => State is RunState.Playing or RunState.Paused;

    /// <summary>Gets whether the chosen character plays, the block's edge then green.</summary>
    public bool IsPlaying => State is RunState.Playing;

    /// <summary>Gets whether the chosen character is paused, the block's edge then amber.</summary>
    public bool IsPaused => State is RunState.Paused;

    /// <summary>Initializes a new instance of the <see cref="RunBlockViewModel"/> class.</summary>
    /// <param name="bots">The bots, whose cards start, pause and stop them as the block does.</param>
    /// <param name="localizer">The app's texts.</param>
    public RunBlockViewModel(BotsViewModel bots, Localizer localizer)
    {
        _bots = bots;
        _localizer = localizer;
        Refresh();
    }

    /// <summary>Reads the chosen character and the characters connected again, after the bots' cards did.</summary>
    public void Refresh()
    {
        var online = _bots.Items.Where(bot => bot.IsConnected).ToList();

        if (!online.SequenceEqual(Online))
        {
            Online.Clear();

            foreach (var bot in online)
                Online.Add(bot);
        }

        IsNobodyOnline = Online.Count == 0;
        Chosen = _chosen is { } key ? _bots.Items.FirstOrDefault(bot => bot.Bot.Key == key) : null;

        foreach (var bot in _bots.Items)
            bot.IsChosen = bot == Chosen;

        if (Chosen is not { } chosen)
        {
            var line = _chosen is { } offline ? Characters?.LineOf(offline) : null;

            State = line is null ? RunState.None : RunState.Stopped;
            Name = line?.Name ?? _localizer["RunNobody"];
            Detail = line is null ? _localizer["RunChoose"] : _localizer["Disconnected"];
            Initials = line?.Initials ?? "";
            Head = line?.Head;
            PortraitColor = line?.PortraitColor;
            CanPlay = false;
            PlayTip = line is null ? _localizer["RunChooseFirst"] : _localizer["Disconnected"];

            return;
        }

        State = !chosen.IsConnected || !chosen.IsActivityRunning ? RunState.Stopped
            : chosen.IsPaused ? RunState.Paused
            : RunState.Playing;
        Name = chosen.Name;
        Initials = chosen.Initials;
        Head = chosen.Head;
        PortraitColor = chosen.PortraitColor;
        Detail = State switch
        {
            RunState.Playing when chosen.Bot.State.IsFighting => _localizer["RunFighting"],
            RunState.Playing => chosen.ProgressText.Length > 0 ? $"{chosen.ActivityName} · {chosen.ProgressText}" : chosen.ActivityName,
            RunState.Paused => _localizer.Format("RunPausedDoing", chosen.ActivityName),
            _ when !chosen.IsConnected => _localizer["Disconnected"],
            _ => _localizer["RunStopped"],
        };
        CanPlay = State is RunState.Paused || (State is RunState.Stopped && chosen.IsConnected && chosen.StartCommand.CanExecute(null));
        PlayTip = State is RunState.Paused ? _localizer["RunResume"]
            : !chosen.IsConnected ? _localizer["Disconnected"]
            : CanPlay ? _localizer.Format("RunStart", chosen.SelectedActivity?.Name ?? "")
            : _localizer["RunNoProfile"];
    }

    /// <summary>Chooses the character the block drives, as the run block's menu does.</summary>
    /// <param name="bot">The character, or <see langword="null"/> for none.</param>
    [RelayCommand]
    public void Choose(BotViewModel? bot)
    {
        Choose(bot?.Bot.Key);
    }

    /// <summary>Chooses the character the block drives, online or not.</summary>
    /// <param name="key">The character, or <see langword="null"/> for none.</param>
    public void Choose(CharacterKey? key)
    {
        _chosen = key;
        Chose?.Invoke(key);
        Refresh();
    }

    /// <summary>Starts the chosen character's activity, or resumes it once paused: F5 when stopped or paused.</summary>
    [RelayCommand]
    public void Play()
    {
        if (!CanPlay || Chosen is not { } chosen)
            return;

        if (State is RunState.Paused)
        {
            chosen.Bot.Resume();
        }
        else
        {
            // The player's hand, or a pause after a fight, may have been left: the activity would start, then wait.
            if (chosen.IsPaused)
                chosen.Bot.Resume();

            chosen.StartCommand.Execute(null);
        }

        Refresh();
    }

    /// <summary>Pauses the chosen character: F6.</summary>
    [RelayCommand]
    public void Pause()
    {
        if (State is not RunState.Playing || Chosen is not { } chosen)
            return;

        chosen.Bot.Pause();
        Refresh();
    }

    /// <summary>Stops the chosen character's activity: Shift F5.</summary>
    [RelayCommand]
    public void Stop()
    {
        if (State is not (RunState.Playing or RunState.Paused) || Chosen is not { } chosen)
            return;

        chosen.StopCommand.Execute(null);
        Refresh();
    }

    /// <summary>Plays or pauses, as F5 does.</summary>
    [RelayCommand]
    public void PlayOrPause()
    {
        if (State is RunState.Playing)
            Pause();
        else
            Play();
    }
}
