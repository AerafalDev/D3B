using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace D3B.App.Controls;

/// <summary>Rounds a window's corners as Windows 11 rounds its own, which next's window has (D545).</summary>
/// <remarks>
/// The studio draws its own title bar, which leaves Windows to choose the corners: it squares them. Asking the desktop window
/// manager for round ones gives the frame next's look; Windows 10 and other systems keep theirs.
/// </remarks>
internal static class WindowCorners
{
    private const int CornerPreference = 33;
    private const int RoundCorners = 2;

    /// <summary>Asks Windows 11 to round a window's corners.</summary>
    /// <param name="window">The window, once it has a handle.</param>
    public static void Round(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || window.TryGetPlatformHandle() is not { } handle)
            return;

        var preference = RoundCorners;

        // A refusal leaves the corners as they were: nothing else depends on them.
        _ = DwmSetWindowAttribute(handle.Handle, CornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
