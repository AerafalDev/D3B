using Avalonia.Animation.Easings;

namespace D3B.App.Controls;

/// <summary>How a part of the studio slides open or closed, next's: 200 ms on cubic-bezier(0.2, 0, 0, 1) (D545).</summary>
/// <remarks>Shown or hidden by a command, a part slides; dragged, it follows the pointer without one, as next's.</remarks>
internal static class DockSlide
{
    private static readonly SplineEasing _curve = new(0.2, 0, 0, 1);

    /// <summary>Gets how long a part takes to open or close.</summary>
    public static TimeSpan Duration { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gives a part's size at a moment of its slide.</summary>
    /// <param name="from">Its size as the slide began.</param>
    /// <param name="to">Its size once open or closed.</param>
    /// <param name="elapsed">The time since the slide began.</param>
    /// <returns>Its size, <paramref name="to"/> once the slide is over.</returns>
    public static double At(double from, double to, TimeSpan elapsed)
    {
        if (elapsed >= Duration)
            return to;

        var progress = Math.Max(0, elapsed / Duration);

        return from + ((to - from) * _curve.Ease(progress));
    }
}
