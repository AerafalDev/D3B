using Avalonia.Headless;
using Avalonia.Threading;

namespace D3B.App.Tests;

/// <summary>Lets the studio's parts finish sliding open or closed (D545), headless frames coming only when the render timer ticks.</summary>
internal static class Slides
{
    /// <summary>Ticks frames for longer than a slide lasts, then runs what they queued.</summary>
    public static void Finish()
    {
        for (var frame = 0; frame < 40; frame++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
