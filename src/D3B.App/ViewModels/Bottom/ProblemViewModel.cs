using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Bottom;

/// <summary>A problem of the problems' tab: what is wrong, why, and a button that fixes it or leads to where it is fixed (D327).</summary>
internal sealed partial class ProblemViewModel
{
    private readonly Action? _fix;

    /// <summary>Gets where it comes from.</summary>
    public ProblemOrigin Origin { get; }

    /// <summary>Gets whether it is an error, else a warning.</summary>
    public bool IsError { get; }

    /// <summary>Gets what is wrong.</summary>
    public string Title { get; }

    /// <summary>Gets why, or where.</summary>
    public string Why { get; }

    /// <summary>Gets what its button says, if it has one.</summary>
    public string? FixText { get; }

    /// <summary>Gets whether it is a warning.</summary>
    public bool IsWarning => !IsError;

    /// <summary>Gets whether it has a button.</summary>
    public bool CanFix => _fix is not null && FixText is not null;

    /// <summary>Initializes a new instance of the <see cref="ProblemViewModel"/> class.</summary>
    /// <param name="origin">Where it comes from.</param>
    /// <param name="isError">Whether it is an error, else a warning.</param>
    /// <param name="title">What is wrong.</param>
    /// <param name="why">Why, or where.</param>
    /// <param name="fixText">What its button says, if it has one.</param>
    /// <param name="fix">What its button does, if it has one.</param>
    public ProblemViewModel(ProblemOrigin origin, bool isError, string title, string why, string? fixText, Action? fix)
    {
        Origin = origin;
        IsError = isError;
        Title = title;
        Why = why;
        FixText = fixText;
        _fix = fix;
    }

    [RelayCommand]
    private void Fix()
    {
        _fix?.Invoke();
    }
}
