using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using D3B.Data.Maps;

namespace D3B.App.Controls;

/// <summary>A map's own cells, drawn flat, one of which is picked by a press (D121, D377).</summary>
/// <remarks>
/// It draws the cells as the live map does, in the same layout, with the cells that leave the map and the elements that stand
/// on it marked, so that an exit or a door is found at a glance. The control keeps nothing: the cell it reports is the
/// owner's to hold.
/// </remarks>
internal sealed class CellPicker : Control
{
    /// <summary>Defines the <see cref="Map"/> property.</summary>
    public static readonly StyledProperty<MapData?> MapProperty = AvaloniaProperty.Register<CellPicker, MapData?>(nameof(Map));

    /// <summary>Defines the <see cref="Palette"/> property.</summary>
    public static readonly StyledProperty<MapPalette?> PaletteProperty = AvaloniaProperty.Register<CellPicker, MapPalette?>(nameof(Palette));

    /// <summary>Defines the <see cref="Selected"/> property.</summary>
    public static readonly StyledProperty<int?> SelectedProperty = AvaloniaProperty.Register<CellPicker, int?>(nameof(Selected));

    private int _hovered = -1;

    /// <summary>Gets or sets the map whose cells are drawn.</summary>
    public MapData? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <summary>Gets or sets the theme's colours.</summary>
    public MapPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>Gets or sets the cell picked, or <see langword="null"/>.</summary>
    public int? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    /// <summary>Gets the cell under the pointer, or -1.</summary>
    public int Hovered => _hovered;

    static CellPicker()
    {
        AffectsRender<CellPicker>(MapProperty, PaletteProperty, SelectedProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CellPicker>(true);
        FocusableProperty.OverrideDefaultValue<CellPicker>(true);
    }

    /// <summary>Raised when a cell is pressed.</summary>
    public event EventHandler<int>? CellPressed;

    /// <summary>Raised when the pointer moves onto another cell, or off the map (-1).</summary>
    public event EventHandler<int>? CellHovered;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Map is not { } map || Palette is not { } palette)
            return;

        var layout = MapLayout.Fit(Bounds.Size);

        if (layout.CellWidth <= 0)
            return;

        var cells = map.Cells.Span;
        var half = new Size(layout.CellWidth / 2, layout.CellHeight / 2);
        var grid = new Pen(palette.Grid, 1);
        var elements = new HashSet<int>();

        foreach (var interactive in map.Interactives.Span)
        {
            if (interactive.CellId != MapInteractive.NoCell)
                elements.Add(interactive.CellId);
        }

        for (var cell = 0; cell < cells.Length; cell++)
        {
            var center = layout.Center(cell);
            var leaves = cells[cell].IsWalkable && Leaves(cells[cell]);
            var fill = !cells[cell].IsWalkable ? palette.Obstacle : leaves ? palette.ExitFill : palette.Walkable;

            context.DrawGeometry(fill, grid, Diamond(center, half));

            if (elements.Contains(cell))
                context.DrawEllipse(palette.Element, null, center, layout.CellHeight / 4, layout.CellHeight / 4);
        }

        if (_hovered >= 0)
            context.DrawGeometry(null, new Pen(palette.Hover, 1.5), Diamond(layout.Center(_hovered), half));

        if (Selected is { } selected && selected >= 0 && selected < cells.Length)
            context.DrawGeometry(null, new Pen(palette.CharacterRing, 2.5), Diamond(layout.Center(selected), half));
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var cell = Map is null ? -1 : MapLayout.Fit(Bounds.Size).CellAt(e.GetPosition(this));

        Hover(cell);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(-1);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Map is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var cell = MapLayout.Fit(Bounds.Size).CellAt(e.GetPosition(this));

        if (cell >= 0)
        {
            Selected = cell;
            CellPressed?.Invoke(this, cell);
            e.Handled = true;
        }
    }

    private static bool Leaves(MapCell cell)
    {
        return cell.MapChangeData != 0;
    }

    private static StreamGeometry Diamond(Point center, Size half)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
            MapMarks.AddDiamond(draw, center, half.Width, half.Height);

        return geometry;
    }

    private void Hover(int cell)
    {
        if (cell == _hovered)
            return;

        _hovered = cell;
        InvalidateVisual();
        CellHovered?.Invoke(this, cell);
    }
}
