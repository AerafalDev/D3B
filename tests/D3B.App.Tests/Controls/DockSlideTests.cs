using D3B.App.Controls;

namespace D3B.App.Tests.Controls;

public sealed class DockSlideTests
{
    [Fact]
    public void APartSlidesFromItsSizeToItsTargetIn200MillisecondsFastFirst()
    {
        // next's curve, cubic-bezier(0.2, 0, 0, 1): most of the way is done early, the end eases out.
        Assert.Equal(0, DockSlide.At(0, 300, TimeSpan.Zero));
        Assert.InRange(DockSlide.At(0, 300, TimeSpan.FromMilliseconds(100)), 240, 300);
        Assert.Equal(300, DockSlide.At(0, 300, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(300, DockSlide.At(0, 300, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void APartClosingSlidesToNothing()
    {
        Assert.InRange(DockSlide.At(340, 0, TimeSpan.FromMilliseconds(50)), 0, 340);
        Assert.Equal(0, DockSlide.At(340, 0, TimeSpan.FromMilliseconds(200)));
    }
}
