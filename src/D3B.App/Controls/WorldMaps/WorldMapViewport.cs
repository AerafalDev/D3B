namespace D3B.App.Controls.WorldMaps;

/// <summary>Where a world's image lies in the view: a scale and an offset, and the maths between the view's pixels and the image's (D308).</summary>
/// <remarks>
/// The scale is the view's pixels per pixel of the client's full-size image, so that 1 draws the image as the client does at its
/// finest. Pure arithmetic, no drawing: the view's zoom, pan and fit are tested here.
/// </remarks>
internal sealed class WorldMapViewport
{
    /// <summary>The least scale: a world's whole image still shows in a small view.</summary>
    public const double MinScale = 0.05;

    /// <summary>The most scale: the finest zoom the client has, enlarged once more.</summary>
    public const double MaxScale = 2.5;

    /// <summary>Gets the view's width in pixels.</summary>
    public double Width { get; private set; }

    /// <summary>Gets the view's height in pixels.</summary>
    public double Height { get; private set; }

    /// <summary>Gets the view's pixels per pixel of the full-size image.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>Gets the view's column of the image's left edge.</summary>
    public double OffsetX { get; private set; }

    /// <summary>Gets the view's row of the image's top edge.</summary>
    public double OffsetY { get; private set; }

    /// <summary>Sets the view's size.</summary>
    /// <param name="width">Its width in pixels.</param>
    /// <param name="height">Its height in pixels.</param>
    public void Resize(double width, double height)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    /// <summary>Gets the pixel of the full-size image that a point of the view shows.</summary>
    /// <param name="x">The view's column.</param>
    /// <param name="y">The view's row.</param>
    /// <returns>The image's column and row.</returns>
    public (double X, double Y) ToImage(double x, double y)
    {
        return ((x - OffsetX) / Scale, (y - OffsetY) / Scale);
    }

    /// <summary>Gets where a pixel of the full-size image lies in the view.</summary>
    /// <param name="x">The image's column.</param>
    /// <param name="y">The image's row.</param>
    /// <returns>The view's column and row.</returns>
    public (double X, double Y) ToView(double x, double y)
    {
        return (OffsetX + (x * Scale), OffsetY + (y * Scale));
    }

    /// <summary>Gets the part of the full-size image the view shows.</summary>
    /// <returns>Its left, top, right and bottom.</returns>
    public (double Left, double Top, double Right, double Bottom) VisibleImage()
    {
        var (left, top) = ToImage(0, 0);
        var (right, bottom) = ToImage(Width, Height);

        return (left, top, right, bottom);
    }

    /// <summary>Moves the image.</summary>
    /// <param name="dx">Columns to move it right.</param>
    /// <param name="dy">Rows to move it down.</param>
    public void Pan(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
    }

    /// <summary>Changes the scale, keeping the image pixel under a point of the view where it is.</summary>
    /// <param name="x">The view's column that stays.</param>
    /// <param name="y">The view's row that stays.</param>
    /// <param name="scale">The new scale, held between <see cref="MinScale"/> and <see cref="MaxScale"/>.</param>
    public void ZoomAt(double x, double y, double scale)
    {
        var (imageX, imageY) = ToImage(x, y);

        Scale = Math.Clamp(scale, MinScale, MaxScale);
        OffsetX = x - (imageX * Scale);
        OffsetY = y - (imageY * Scale);
    }

    /// <summary>Shows a part of the image whole, centred.</summary>
    /// <param name="left">The part's left in the full-size image.</param>
    /// <param name="top">Its top.</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="margin">Pixels of the view left around it.</param>
    public void Fit(double left, double top, double width, double height, double margin)
    {
        var availableWidth = Width - (2 * margin);
        var availableHeight = Height - (2 * margin);

        if (width <= 0 || height <= 0 || availableWidth <= 0 || availableHeight <= 0)
            return;

        Scale = Math.Clamp(Math.Min(availableWidth / width, availableHeight / height), MinScale, MaxScale);
        OffsetX = ((Width - (width * Scale)) / 2) - (left * Scale);
        OffsetY = ((Height - (height * Scale)) / 2) - (top * Scale);
    }

    /// <summary>Keeps some of the image in the view, so that panning never loses it.</summary>
    /// <param name="imageWidth">The image's width at full size.</param>
    /// <param name="imageHeight">The image's height at full size.</param>
    /// <param name="kept">Pixels of the view that the image keeps at least.</param>
    public void Keep(double imageWidth, double imageHeight, double kept)
    {
        var keptX = Math.Min(kept, Width);
        var keptY = Math.Min(kept, Height);

        OffsetX = Math.Clamp(OffsetX, keptX - (imageWidth * Scale), Width - keptX);
        OffsetY = Math.Clamp(OffsetY, keptY - (imageHeight * Scale), Height - keptY);
    }
}
