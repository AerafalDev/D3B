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

            view.StationSelected += (station, _) => chosen.Add(station);

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
    public async Task APressOnAStationWithControlOrShiftTellsTheKeysAndDoesNotCarryTheStationOff()
    {
        var chosen = new List<(int Station, KeyModifiers Keys)>();
        var moved = new List<int>();

        await OnTheUIThreadAsync(async () =>
        {
            var scene = new WorldMapScene([new WorldMapStation(1, 0, "A", false, false), new WorldMapStation(4, 0, "B", false, false)], []);
            var (window, view) = Show(Source(RedImage(), new int[1]), scene);

            view.StationSelected += (station, keys) => chosen.Add((station, keys));
            view.StationMoved += (station, _, _) => moved.Add(station);

            var station = view.RectOf(4, 0).Center;
            var next = view.RectOf(3, 0).Center;

            window.MouseDown(station, MouseButton.Left, RawInputModifiers.Control);
            window.MouseMove(next, RawInputModifiers.Control);
            window.MouseUp(next, MouseButton.Left, RawInputModifiers.Control);
            station = view.RectOf(4, 0).Center;

            window.MouseDown(station, MouseButton.Left, RawInputModifiers.Shift);
            window.MouseUp(station, MouseButton.Left, RawInputModifiers.Shift);
            await Task.Yield();

            return true;
        });

        Assert.Equal([(1, KeyModifiers.Control), (1, KeyModifiers.Shift)], chosen);
        Assert.Empty(moved);
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

    private static RenderTargetBitmap GreenPicture()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        var green = new Border { Width = 64, Height = 64, Background = Brushes.Lime };

        green.Measure(new Size(64, 64));
        green.Arrange(new Rect(0, 0, 64, 64));
        bitmap.Render(green);

        return bitmap;
    }

    [Fact]
    public async Task AHintIsDrawnWithItsPictureAndHoveringItIsAHitOnTheHintAndNotOnTheSquare()
    {
        var (before, over, after, middle) = await OnTheUIThreadAsync(async () =>
        {
            var (window, view) = Show(Source(RedImage(), new int[1]));
            var picture = GreenPicture();
            var center = view.RectOf(3, 0).Center;

            await SettleAsync(() => PixelAt(window, (int)center.X, (int)center.Y).Red > 200);

            var plain = PixelAt(window, (int)center.X, (int)center.Y);

            view.Hints = [new WorldMapHint(7, "Zaap", "Transports", 0, 3, 0, 100, () => picture)];
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(center);
            await Task.Yield();

            var hit = view.Hit;
            var drawn = PixelAt(window, (int)center.X, (int)center.Y);

            view.Hints = [];
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(center + new Vector(1, 0));
            await Task.Yield();

            return (plain, hit, view.Hit, drawn);
        });

        Assert.True(before.Red > 200, $"The map's tile under the hint: {before}.");
        Assert.Equal((WorldMapHitKind.Hint, "7", 3, 0), (over.Kind, over.Id, over.X, over.Y));
        Assert.True(middle.Green > 200 && middle.Red < 80, $"The hint's picture over the tile: {middle}.");
        Assert.Equal(WorldMapHitKind.Square, after.Kind);
    }

    [Fact]
    public async Task APressOnAHintWithTheTraceToolIsAPressOnItsMapAndWithTheSelectToolItMovesTheImage()
    {
        var traced = new List<(int X, int Y, bool IsDrag)>();
        var (moved, hit) = await OnTheUIThreadAsync(async () =>
        {
            var (window, view) = Show(Source(RedImage(), new int[1]));
            var picture = GreenPicture();
            var center = view.RectOf(3, 0).Center;

            view.Hints = [new WorldMapHint(7, "Zaap", "Transports", 0, 3, 0, 100, () => picture)];
            view.SquareTraced += (x, y, isDrag) => traced.Add((x, y, isDrag));
            Dispatcher.UIThread.RunJobs();

            // The select tool: the press is the start of a drag of the image.
            var origin = view.RectOf(1, 0).Center;

            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Vector(-40, 30));
            window.MouseUp(center + new Vector(-40, 30), MouseButton.Left);
            await Task.Yield();

            var shifted = view.RectOf(1, 0).Center - origin;

            view.Tool = WorldMapTool.Trace;

            var again = view.RectOf(3, 0).Center;

            window.MouseDown(again, MouseButton.Left);
            window.MouseUp(again, MouseButton.Left);
            await Task.Yield();

            return (shifted, view.Hit);
        });

        Assert.Equal((-40, 30), (Math.Round(moved.X), Math.Round(moved.Y)));
        Assert.Equal([(3, 0, false)], traced);
        Assert.Equal(WorldMapHitKind.Hint, hit.Kind);
    }
}
