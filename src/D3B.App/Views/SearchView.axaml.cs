using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.ViewModels.Studio;

namespace D3B.App.Views;

/// <summary>The search's palette, Ctrl K: its field keeps the keyboard, the arrows, Enter, Tab and Escape acting on the results (D328).</summary>
internal sealed partial class SearchView : UserControl
{
    private SearchViewModel? _search;

    /// <summary>Initializes a new instance of the <see cref="SearchView"/> class.</summary>
    public SearchView()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_search is not null)
            _search.PropertyChanged -= OnSearchChanged;

        _search = DataContext as SearchViewModel;

        if (_search is not null)
            _search.PropertyChanged += OnSearchChanged;
    }

    private void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Opened, the field takes the keyboard once it shows.
        if (e.PropertyName == nameof(SearchViewModel.IsOpen) && _search is { IsOpen: true })
            Dispatcher.UIThread.Post(() => QueryBox.Focus(), DispatcherPriority.Input);
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (_search is null)
            return;

        switch (e.Key)
        {
            case Key.Down:
                _search.Move(1);
                break;
            case Key.Up:
                _search.Move(-1);
                break;
            case Key.Enter:
                _search.RunActive();
                break;
            case Key.Tab:
                _search.Cycle(!e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                break;
            case Key.Escape:
                _search.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
        Dispatcher.UIThread.Post(BringActiveIntoView, DispatcherPriority.Background);
    }

    private void BringActiveIntoView()
    {
        var active = Results.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.DataContext is SearchResult { IsActive: true });

        active?.BringIntoView();
    }

    private void OnScopeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SearchScopeChip chip })
            _search?.Choose(chip.Scope);

        QueryBox.Focus();
    }

    private void OnResultPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: SearchResult result })
            _search?.Activate(result);
    }

    private void OnResultPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: SearchResult result })
        {
            e.Handled = true;
            _search?.Run(result);
        }
    }
}
