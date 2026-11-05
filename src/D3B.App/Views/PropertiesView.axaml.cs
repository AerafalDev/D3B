using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Properties;

namespace D3B.App.Views;

/// <summary>The inspector: a character's or an asset's properties, in sections under a rule, the labels' column resized by its edge (D324).</summary>
internal sealed partial class PropertiesView : UserControl
{
    // A row's margins and its reset's column, PropertiesView.axaml's: the labels' share is of what is left between them.
    private const double RowStart = 14;
    private const double RowEnd = 8 + 20;

    private PropertiesViewModel? _shown;
    private bool _dragging;

    /// <summary>Initializes a new instance of the <see cref="PropertiesView"/> class.</summary>
    public PropertiesView()
    {
        InitializeComponent();
    }

    private void OnNameLostFocus(object? sender, RoutedEventArgs e)
    {
        (DataContext as PropertiesViewModel)?.Asset.CommitNameCommand.Execute(null);
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_shown is not null)
            _shown.PropertyChanged -= OnShownChanged;

        _shown = DataContext as PropertiesViewModel;

        if (_shown is not null)
            _shown.PropertyChanged += OnShownChanged;

        PlaceEdge();
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PlaceEdge();
    }

    private static MenuItem Item(string text, Action run)
    {
        var item = new MenuItem { Header = text };

        item.Click += (_, _) => run();

        return item;
    }

    private void OnShownChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PropertiesViewModel.LabelShare))
            PlaceEdge();
    }

    private double RowWidth()
    {
        return Math.Max(1, Bounds.Width - RowStart - RowEnd);
    }

    private void PlaceEdge()
    {
        if (_shown is not null)
            LabelEdge.Margin = new Thickness(RowStart + (_shown.LabelShare * RowWidth()) - 3, 0, 0, 0);
    }

    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        _dragging = true;
        e.Pointer.Capture(LabelEdge);
        e.Handled = true;
    }

    private void OnEdgeMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging && _shown is not null)
            _shown.LabelShare = (e.GetPosition(this).X - RowStart) / RowWidth();
    }

    private void OnEdgeReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void OnProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_shown?.Character is not { } character || sender is not Control field)
            return;

        // Made as it opens, as the cards' menu: the profiles may have changed since.
        var texts = Localizer.Instance;
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft };

        if (character.HasProfile)
        {
            menu.Items.Add(Item(texts["OpenProfile"], () => character.OpenProfileCommand.Execute(null)));
            menu.Items.Add(new Separator());
        }

        void Group(string heading, IReadOnlyList<ProfileChoice> choices)
        {
            if (choices.Count == 0)
                return;

            menu.Items.Add(new MenuItem { Header = heading, Classes = { "Heading" } });

            foreach (var choice in choices)
                menu.Items.Add(new MenuItem { Header = choice.Name, Tag = choice.Detail, Command = character.AssignCommand, CommandParameter = choice });
        }

        Group(texts["ProfilesMine"], character.MyProfiles);
        Group(texts["ProfilesGiven"], character.GivenProfiles);
        Group(texts["ProfilesActivities"], character.ActivityProfiles);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["NoProfile"], () => character.AssignCommand.Execute(null)));
        field.ContextMenu = menu;
        menu.Closed += (_, _) => field.ContextMenu = null;
        menu.Open(field);
    }
}
