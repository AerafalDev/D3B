using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace D3B.App.Tests;

/// <summary>Keeps a window drawn without a screen as an image, to look at, when <c>D3B_DRAWINGS</c> names a folder; else nothing.</summary>
internal static class Drawings
{
    public static void Save(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("D3B_DRAWINGS") is not { Length: > 0 } folder)
            return;

        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
