using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Assets;
using D3B.Bot.Characters;
using D3B.Bot.Hosting;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.App.ViewModels.Bottom;

/// <summary>The problems, in the bottom panel: by origin, the project, the characters and the host, each saying what is wrong, why, and how to fix it (D327).</summary>
/// <remarks>
/// <para>
/// Made again only once the project, the characters, the host's problems, the language or the filter changed: the tab's
/// title counts them whichever tab shows, so the panel reads them at each refresh while it is open.
/// </para>
/// <para>
/// The host's warnings and errors are told once each, the latest kept, the most recent <see cref="HostShown"/>: the journal
/// keeps them all.
/// </para>
/// </remarks>
internal sealed partial class ProblemsViewModel : ObservableObject
{
    /// <summary>The most host problems shown, the latest.</summary>
    public const int HostShown = 50;

    private readonly Localizer _localizer;
    private (int Assets, int Accounts, long Host, string? Failure, CultureInfo? Culture, ProblemOrigin? Filter) _read = (-1, -1, -1, null, null, null);
    private List<ProblemViewModel> _all = [];

    /// <summary>Gets the groups the filter lets through, each with its problems.</summary>
    public ObservableCollection<ProblemGroupViewModel> Groups { get; } = [];

    /// <summary>Gets the origin shown, or <see langword="null"/> for every one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsProject), nameof(IsCharacters), nameof(IsHost))]
    public partial ProblemOrigin? Filter { get; private set; }

    /// <summary>Gets how many errors there are, whatever the filter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial int Errors { get; private set; }

    /// <summary>Gets how many warnings there are, whatever the filter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial int Warnings { get; private set; }

    /// <summary>Gets whether no problem passes the filter.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets or sets what opens a folder in the system's file manager, as the window can: a file that does not read is fixed there.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>Gets or sets what chooses a character, so that the inspector shows it and its profile is given there.</summary>
    public Action<CharacterKey>? ChooseCharacter { get; set; }

    /// <summary>Gets or sets what shows the host's lines in the journal.</summary>
    public Action? ShowHostJournal { get; set; }

    /// <summary>Gets whether every origin shows.</summary>
    public bool IsAll => Filter is null;

    /// <summary>Gets whether the project's problems alone show.</summary>
    public bool IsProject => Filter is ProblemOrigin.Project;

    /// <summary>Gets whether the characters' problems alone show.</summary>
    public bool IsCharacters => Filter is ProblemOrigin.Characters;

    /// <summary>Gets whether the host's problems alone show.</summary>
    public bool IsHost => Filter is ProblemOrigin.Host;

    /// <summary>Gets whether there is an error, which the tab's title counts in red.</summary>
    public bool HasErrors => Errors > 0;

    /// <summary>Gets whether there is a warning, which the tab's title counts in amber.</summary>
    public bool HasWarnings => Warnings > 0;

    /// <summary>Initializes a new instance of the <see cref="ProblemsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    public ProblemsViewModel(Localizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Reads the problems again, once anything they come from changed.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was read stays.</param>
    /// <param name="hostFailure">Why the host did not start, once it failed.</param>
    public void Refresh(IServiceProvider? services, string? hostFailure)
    {
        var assets = services?.GetService<AssetLibrary>();
        var book = services?.GetService<AccountBook>();
        var host = services?.GetService<HostProblems>();
        var read = (assets?.Version ?? _read.Assets, book?.Version ?? _read.Accounts, host?.Problems.Written ?? _read.Host, hostFailure, _localizer.Culture, Filter);

        if (read == _read)
            return;

        _read = read;

        // A host stopped keeps the problems it had: only a new read replaces them.
        if (services is not null)
        {
            _all = [.. ProjectProblems(assets), .. CharacterProblems(assets, book), .. HostProblemsOf(host, hostFailure)];
        }
        else if (hostFailure is not null)
        {
            _all = [.. _all.Where(problem => problem.Origin is not ProblemOrigin.Host), .. HostProblemsOf(null, hostFailure)];
        }

        Show();
    }

    private void Show()
    {
        Errors = _all.Count(problem => problem.IsError);
        Warnings = _all.Count - Errors;
        Groups.Clear();

        foreach (var origin in Enum.GetValues<ProblemOrigin>())
        {
            if (Filter is { } only && only != origin)
                continue;

            var problems = _all.Where(problem => problem.Origin == origin).OrderByDescending(problem => problem.IsError).ToList();

            if (problems.Count > 0)
                Groups.Add(new ProblemGroupViewModel(_localizer["ProblemsOrigin" + origin], problems));
        }

        IsEmpty = Groups.Count is 0;
    }

    private IEnumerable<ProblemViewModel> ProjectProblems(AssetLibrary? assets)
    {
        foreach (var problem in assets?.Problems ?? [])
        {
            var folder = Path.GetDirectoryName(Path.Combine(assets!.Folder, problem.Path)) ?? assets.Folder;

            yield return new ProblemViewModel(ProblemOrigin.Project, true, _localizer.Format("ProblemAssetNotRead", Path.GetFileName(problem.Path)), problem.Path + " · " + problem.Message, _localizer["ProblemOpenFolder"], () => OpenFolder?.Invoke(folder));
        }
    }

    private IEnumerable<ProblemViewModel> CharacterProblems(AssetLibrary? assets, AccountBook? book)
    {
        if (assets is null || book is null)
            yield break;

        foreach (var character in book.Characters.OrderBy(character => character.Name, StringComparer.Create(_localizer.Culture, true)))
        {
            var key = character.Key;

            if (character.Profile is null)
                yield return new ProblemViewModel(ProblemOrigin.Characters, false, _localizer.Format("ProblemNoProfile", character.Name), _localizer["ProblemNoProfileWhy"], _localizer["ProblemChooseProfile"], () => ChooseCharacter?.Invoke(key));
            else if (assets.Find(character.Profile) is null)
                yield return new ProblemViewModel(ProblemOrigin.Characters, true, _localizer.Format("ProblemProfileGone", character.Name), _localizer["ProblemProfileGoneWhy"], _localizer["ProblemChooseProfile"], () => ChooseCharacter?.Invoke(key));
        }
    }

    private IEnumerable<ProblemViewModel> HostProblemsOf(HostProblems? host, string? failure)
    {
        if (failure is not null)
            yield return new ProblemViewModel(ProblemOrigin.Host, true, _localizer["ProblemHostFailed"], failure.Split('\n', 2)[0].TrimEnd('\r'), null, null);

        if (host is null)
            yield break;

        // The same message, logged again and again, is one problem: its latest time says it.
        var latest = host.Problems.All().Reverse().DistinctBy(problem => problem.Message).Take(HostShown);

        foreach (var problem in latest)
        {
            var category = problem.Category[(problem.Category.LastIndexOf('.') + 1)..];
            var why = category + " · " + problem.At.ToLocalTime().ToString("T", _localizer.Culture);

            yield return new ProblemViewModel(ProblemOrigin.Host, problem.Level >= LogLevel.Error, problem.Message.Split('\n', 2)[0].TrimEnd('\r'), why, _localizer["ProblemSeeJournal"], () => ShowHostJournal?.Invoke());
        }
    }

    private void ShowOnly(ProblemOrigin? origin)
    {
        Filter = origin;
        _read = _read with { Filter = origin };
        Show();
    }

    [RelayCommand]
    private void ShowAll()
    {
        ShowOnly(null);
    }

    [RelayCommand]
    private void ShowProject()
    {
        ShowOnly(ProblemOrigin.Project);
    }

    [RelayCommand]
    private void ShowCharacters()
    {
        ShowOnly(ProblemOrigin.Characters);
    }

    [RelayCommand]
    private void ShowHost()
    {
        ShowOnly(ProblemOrigin.Host);
    }
}
