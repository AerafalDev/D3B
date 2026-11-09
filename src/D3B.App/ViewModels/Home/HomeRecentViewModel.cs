using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Home;

/// <summary>An asset of the project on the home page, listed by when it last changed: a click opens it (D406).</summary>
internal sealed partial class HomeRecentViewModel
{
    private readonly Action<string> _open;

    /// <summary>Gets the asset's id.</summary>
    public string Id { get; }

    /// <summary>Gets the asset's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets what the asset is, said.</summary>
    public required string Kind { get; init; }

    /// <summary>Gets the kind's IntelliJ icon by its key.</summary>
    public required string Icon { get; init; }

    /// <summary>Gets the kind's colour, its icon's.</summary>
    public required IBrush KindBrush { get; init; }

    /// <summary>Gets the kind's dark tint, its icon's square.</summary>
    public required IBrush KindTint { get; init; }

    /// <summary>Gets when the asset last changed, said.</summary>
    public required string When { get; init; }

    /// <summary>Initializes a new instance of the <see cref="HomeRecentViewModel"/> class.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="open">Opens the asset.</param>
    public HomeRecentViewModel(string id, Action<string> open)
    {
        _open = open;
        Id = id;
    }

    [RelayCommand]
    private void Open()
    {
        _open(Id);
    }
}
