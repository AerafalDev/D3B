using Avalonia.Controls;
using Avalonia.Interactivity;
using D3B.App.ViewModels.Characters;

namespace D3B.App.Views;

/// <summary>A character's tab: a header to choose its profile and play it, its map live, and a column of what it does, its behavior, its session and its latest decisions (D325, D514).</summary>
internal sealed partial class CharacterView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="CharacterView"/> class.</summary>
    public CharacterView()
    {
        InitializeComponent();
    }

    private void OnProfileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CharacterTabViewModel { Character: { } character } && sender is Control field)
            ProfileMenu.Open(character, field);
    }
}
