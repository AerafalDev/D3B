using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace D3B.App.Tests;

/// <summary>Draws a window without a screen, dark as the studio is (D320), kept as an image to look at when <c>D3B_DRAWINGS</c> names a folder.</summary>
internal static class Drawings
{
    public static void Draw(Window window, string name)
    {
        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);

        if (Folder() is { } folder)
            frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    /// <summary>Draws a part of a window twice its size, to set beside the mockup's.</summary>
    public static void DrawPart(Control part, string name)
    {
        if (Folder() is not { } folder || part.Bounds.Width < 1 || part.Bounds.Height < 1)
            return;

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(part.Bounds.Width * 2), (int)(part.Bounds.Height * 2)), new Vector(192, 192));

        bitmap.Render(part);
        bitmap.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    private static string? Folder()
    {
        if (Environment.GetEnvironmentVariable("D3B_DRAWINGS") is not { Length: > 0 } folder)
            return null;

        Directory.CreateDirectory(folder);

        return folder;
    }
}
