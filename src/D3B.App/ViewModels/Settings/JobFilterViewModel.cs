using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A profession the resources' dialog filters by, every one, or none for <c>Base</c>'s resources (D150).</summary>
internal sealed partial class JobFilterViewModel : ObservableObject
{
    private readonly Action<JobFilterViewModel> _select;

    /// <summary>Gets the profession, or <see langword="null"/> for every one and for none.</summary>
    public int? Id { get; }

    /// <summary>Gets its name.</summary>
    public string Name { get; }

    /// <summary>Gets its icon, when the pack keeps one.</summary>
    public IImage? Icon { get; }

    /// <summary>Gets or sets whether it stands for the resources of no profession, <c>Base</c>'s.</summary>
    public bool IsBase { get; init; }

    /// <summary>Gets or sets whether it is the filter chosen.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="JobFilterViewModel"/> class.</summary>
    /// <param name="id">The profession, or <see langword="null"/>.</param>
    /// <param name="name">Its name.</param>
    /// <param name="icon">Its icon, or <see langword="null"/>.</param>
    /// <param name="select">Chooses it.</param>
    public JobFilterViewModel(int? id, string name, IImage? icon, Action<JobFilterViewModel> select)
    {
        Id = id;
        Name = name;
        Icon = icon;
        _select = select;
    }

    [RelayCommand]
    private void Choose()
    {
        _select(this);
    }
}
