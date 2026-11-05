using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Tests.Controls;

public sealed class WorldMapViewTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WorldMapViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static byte[] RedImage()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(1000, 800), new Vector(96, 96));

        var red = new Border { Width = 1000, Height = 800, Background = Brushes.Red };

        red.Measure(new Size(1000, 800));
        red.Arrange(new Rect(0, 0, 1000, 800));
        bitmap.Render(red);

        using var stream = new MemoryStream();

        bitmap.Save(stream, PngBitmapEncoderOptions.Default);

        return stream.ToArray();
    }

    private static WorldMapSource Source(byte[] image, int[] reads)
    {
        return new WorldMapSource(TestRouteWorld.Catalog(), TestRouteWorld.Index(), (_, _, _) =>
        {
            Interlocked.Increment(ref reads[0]);

            return image;
        });
    }

    private static async Task SettleAsync(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (Window Window, WorldMapView View) Show(WorldMapSource source, WorldMapScene? scene = null)
    {
        var view = new WorldMapView { Source = source, World = 1, Scene = scene ?? WorldMapScene.Empty };
        var window = new Window { Width = 1000, Height = 800, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static (int Red, int Green, int Blue) PixelAt(Window window, int x, int y)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        var offset = (y * locked.RowBytes) + (x * 4);
        var first = System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset);
        var second = System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset + 1);
        var third = System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset + 2);

        return locked.Format == PixelFormat.Bgra8888 ? (third, second, first) : (first, second, third);
    }

    [Fact]
    public async Task ATileLoadsOffTheInterfaceThreadAndIsDrawnWhereTheImageLies()
    {
        var reads = new int[1];
        var drawn = await OnTheUIThreadAsync(async () =>
        {
            var (window, view) = Show(Source(RedImage(), reads));
            var middle = view.RectOf(7, 8).Center;

            await SettleAsync(() => Volatile.Read(ref reads[0]) > 0 && PixelAt(window, (int)middle.X, (int)middle.Y).Red > 200);

            return PixelAt(window, (int)middle.X, (int)middle.Y);
        });

        Assert.Equal(1, reads[0]);

        // Red where the sea's dark blue was: the picture drawn is the tile's.
        Assert.True(drawn.Red > 200 && drawn.Green < 60 && drawn.Blue < 60, $"Pixel {drawn}.");
    }

    [Fact]
    public async Task APressOnAStationChoosesItAndADragOnTheSeaMovesTheImage()
    {
        var chosen = new List<int>();
        var (before, after) = await OnTheUIThreadAsync(async () =>
        {
            var scene = new WorldMapScene([new WorldMapStation(1, 0, "A", false, false), new WorldMapStation(4, 0, "B", false, false)], []);
            var (window, view) = Show(Source(RedImage(), new int[1]), scene);

            view.StationSelected += chosen.Add;

            var station = view.RectOf(4, 0).Center;

            window.MouseDown(station, MouseButton.Left);
            window.MouseUp(station, MouseButton.Left);

            var sea = new Point(900, 700);
            var origin = view.RectOf(1, 0).Center;

            window.MouseDown(sea, MouseButton.Left);
            window.MouseMove(new Point(850, 650));
            window.MouseUp(new Point(850, 650), MouseButton.Left);
            await Task.Yield();

            return (origin, view.RectOf(1, 0).Center);
        });

        Assert.Equal([1], chosen);
        Assert.Equal((-50, -50), (Math.Round(after.X - before.X), Math.Round(after.Y - before.Y)));
    }

    [Fact]
    public async Task TheTraceToolReportsTheMapPressedAndEachOneADragCrosses()
    {
        var traced = new List<(int X, int Y, bool IsDrag)>();
        var hits = await OnTheUIThreadAsync(async () =>
        {
            var (window, view) = Show(Source(RedImage(), new int[1]));

            view.Tool = WorldMapTool.Trace;
            view.SquareTraced += (x, y, isDrag) => traced.Add((x, y, isDrag));

            window.MouseDown(view.RectOf(1, 0).Center, MouseButton.Left);
            window.MouseMove(view.RectOf(2, 0).Center);
            window.MouseMove(view.RectOf(2, 0).Center + new Vector(3, 0));
            window.MouseMove(view.RectOf(3, 0).Center);
            window.MouseUp(view.RectOf(3, 0).Center, MouseButton.Left);
            await Task.Yield();

            return view.Hit;
        });

        Assert.Equal([(1, 0, false), (2, 0, true), (3, 0, true)], traced);
        Assert.Equal(WorldMapHitKind.Square, hits.Kind);
    }

    [Fact]
    public async Task AFastDragMissesNoMapAndAPillOverAMapDoesNotHideIt()
    {
        var traced = new List<(int X, int Y)>();

        await OnTheUIThreadAsync(async () =>
        {
            var scene = new WorldMapScene([new WorldMapStation(2, 0, "A", false, false)], []);
            var (window, view) = Show(Source(RedImage(), new int[1]), scene);

            view.Tool = WorldMapTool.Trace;
            view.SquareTraced += (x, y, _) => traced.Add((x, y));

            window.MouseDown(view.RectOf(1, 0).Center, MouseButton.Left);
            window.MouseMove(view.RectOf(4, 0).Center);
            window.MouseUp(view.RectOf(4, 0).Center, MouseButton.Left);
            await Task.Yield();

            return 0;
        });

        Assert.Equal([(1, 0), (2, 0), (3, 0), (4, 0)], traced);
    }

    [Fact]
    public async Task TheWheelEasesTheZoomTowardsItsTargetAroundThePointer()
    {
        var (start, end) = await OnTheUIThreadAsync(async () =>
        {
            var (window, view) = Show(Source(RedImage(), new int[1]));
            var first = view.ZoomPercent;

            window.MouseWheel(new Point(500, 400), new Vector(0, 2));
            await SettleAsync(() => view.ZoomPercent > first * 1.4);

            return (first, view.ZoomPercent);
        });

        Assert.True(end > start * 1.4, $"{start} to {end}.");
    }
}
