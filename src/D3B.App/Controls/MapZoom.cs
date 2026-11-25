namespace D3B.App.Controls;

/// <summary>How far a character's map is zoomed in its control, and which part of it shows (D550).</summary>
/// <remarks>
/// <para>
/// The scale is a multiple of the map fitted to the control (<see cref="MapLayout.Fit"/>), so that 1 is the whole map and the
/// zoom never shows less than it. A point of the fitted control lies at its offset plus its coordinates times the scale.
/// </para>
/// <para>
/// The zoomed control always covers the view: a drag cannot lose the map, and at the fit there is nothing to pan. Pure
/// arithmetic, no drawing, as <see cref="WorldMaps.WorldMapViewport"/> is for the world: the zoom, the pan and the fit are tested here.
/// </para>
/// </remarks>
internal sealed class MapZoom
{
    /// <summary>The least scale: the map fitted to the control.</summary>
    public const double MinScale = 1;

    /// <summary>The most scale: under four cells across the control, enough to read a crowded cell.</summary>
    public const double MaxScale = 4;

    private double _width;
    private double _height;

    /// <summary>Gets how many times the fitted map is enlarged.</summary>
    public double Scale { get; private set; } = MinScale;

    /// <summary>Gets the view's column of the zoomed control's left edge, zero or less.</summary>
    public double OffsetX { get; private set; }

    /// <summary>Gets the view's row of the zoomed control's top edge, zero or less.</summary>
    public double OffsetY { get; private set; }

    /// <summary>Sets the control's size, keeping the zoomed map covering it.</summary>
    /// <param name="width">Its width in pixels.</param>
    /// <param name="height">Its height in pixels.</param>
    public void Resize(double width, double height)
    {
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);
        Keep();
    }

    /// <summary>Changes the scale, keeping the point of the map under a point of the view where it is, as far as the edges allow.</summary>
    /// <param name="x">The view's column that stays.</param>
    /// <param name="y">The view's row that stays.</param>
    /// <param name="scale">The new scale, held between <see cref="MinScale"/> and <see cref="MaxScale"/>.</param>
    public void ZoomAt(double x, double y, double scale)
    {
        var fittedX = (x - OffsetX) / Scale;
        var fittedY = (y - OffsetY) / Scale;

        Scale = Math.Clamp(scale, MinScale, MaxScale);
        OffsetX = x - (fittedX * Scale);
        OffsetY = y - (fittedY * Scale);
        Keep();
    }

    /// <summary>Moves the zoomed map, as far as its edges allow.</summary>
    /// <param name="dx">Pixels to move it right.</param>
    /// <param name="dy">Pixels to move it down.</param>
    public void Pan(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        Keep();
    }

    /// <summary>Shows the whole map again.</summary>
    public void Fit()
    {
        Scale = MinScale;
        OffsetX = 0;
        OffsetY = 0;
    }

    /// <summary>Gets where the cells lie once zoomed, without the offset: the layout the geometries are built for, drawn moved by the offset.</summary>
    /// <param name="fitted">The map fitted to the control.</param>
    /// <returns>The layout scaled about the control's corner.</returns>
    public MapLayout Scaled(MapLayout fitted)
    {
        return new MapLayout(fitted.Left * Scale, fitted.Top * Scale, fitted.CellWidth * Scale);
    }

    /// <summary>Gets where the cells lie in the view once zoomed and moved.</summary>
    /// <param name="fitted">The map fitted to the control.</param>
    /// <returns>The layout the pointer finds its cell in.</returns>
    public MapLayout Apply(MapLayout fitted)
    {
        var scaled = Scaled(fitted);

        return new MapLayout(OffsetX + scaled.Left, OffsetY + scaled.Top, scaled.CellWidth);
    }

    private void Keep()
    {
        OffsetX = Math.Clamp(OffsetX, _width - (_width * Scale), 0);
        OffsetY = Math.Clamp(OffsetY, _height - (_height * Scale), 0);
    }
}
