using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Zones;
using D3B.Studio.Assets;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class ZoneEditorViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<(ZonesViewModel Zones, ZoneEditorViewModel Editor)> OpenAsync(ZoneAsset? zone = null)
    {
        await _store.OpenedAsync();

        var services = TestRouteWorld.Services(_store.Assets);
        var zones = new ZonesViewModel(_localizer, new Dialogs(), new Toasts(), () => services);
        var saved = await _store.Assets.SaveAsync(zone ?? new ZoneAsset { Id = "z1", Name = "North" }, TestContext.Current.CancellationToken);
        var editor = zones.OpenZone(saved.Id)!;

        return (zones, editor);
    }

    private static List<(int X, int Y, bool IsLeftOut)> Squares(ZoneEditorViewModel editor)
    {
        return [.. editor.Scene.Squares!.Select(square => (square.X, square.Y, square.IsLeftOut)).OrderBy(square => square.X).ThenBy(square => square.Y)];
    }

    [Fact]
    public async Task PaintingTakesTheFirstMapOfASquareTheWorldsBeforeTheInsidesAndAMapAlreadyInIsLeftAlone()
    {
        var (_, editor) = await OpenAsync();

        Assert.True(editor.IsEmpty);
        Assert.Equal(WorldMapTool.Trace, editor.Tool);

        // Square 2, 0 holds map 2 and the inside, map 9.
        editor.TraceSquare(2, 0, isDrag: false);
        editor.TraceSquare(2, 0, isDrag: false);

        Assert.Equal(1, editor.MapCount);
        Assert.Equal([(2, 0, false)], Squares(editor));
        Assert.Equal("1 map · 0 sub-areas · 0 left out", editor.Summary);
        Assert.True(editor.IsDirty);

        // A square with no map does nothing.
        editor.TraceSquare(9, 9, isDrag: false);

        Assert.Equal(1, editor.MapCount);
    }

    [Fact]
    public async Task AGestureAcrossManyMapsIsOneChangeToUndoAndRedoPutsItBack()
    {
        var (_, editor) = await OpenAsync();

        editor.BeginGesture();
        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(2, 0, isDrag: true);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.EndGesture();
        editor.TraceSquare(4, 0, isDrag: false);

        Assert.Equal(4, editor.MapCount);

        editor.Undo();

        Assert.Equal([(1, 0, false), (2, 0, false), (3, 0, false)], Squares(editor));

        editor.Undo();

        Assert.True(editor.IsEmpty);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);

        editor.Redo();

        Assert.Equal(3, editor.MapCount);
        Assert.True(editor.IsDirty);
        Assert.True(editor.CanRedo);
    }

    [Fact]
    public async Task ASubAreaIsAddedWholeFromOneMapAndTakenOutByTheSameToolOrTheList()
    {
        var (_, editor) = await OpenAsync();

        editor.Tool = WorldMapTool.Region;
        editor.TraceSquare(3, 0, isDrag: false);

        // Sub-area 95: maps 1 to 5 on world 1 and map 7 on world 2, which this layer does not show.
        Assert.Equal(6, editor.MapCount);
        Assert.Equal([(1, 0, false), (2, 0, false), (3, 0, false), (4, 0, false), (5, 0, false)], Squares(editor));
        Assert.Equal([TestRouteWorld.SubAreaId], editor.SubAreas.Select(subArea => subArea.Id));
        Assert.Equal("6 maps · 1 sub-area · 0 left out", editor.Summary);
        Assert.True(editor.HasSubAreas);

        // A drag does not toggle it again.
        editor.TraceSquare(4, 0, isDrag: true);

        Assert.Equal(6, editor.MapCount);

        editor.TraceSquare(1, 0, isDrag: false);

        Assert.True(editor.IsEmpty);
        Assert.False(editor.HasSubAreas);

        editor.TraceSquare(1, 0, isDrag: false);
        editor.SubAreas.Single().RemoveCommand.Execute(null);

        Assert.True(editor.IsEmpty);
    }

    [Fact]
    public async Task ErasingAMapOfASubAreaLeavesItOutAndPaintingItBackTakesItBackInAndTakingTheSubAreaOutForgetsWhatWasLeftOut()
    {
        var (_, editor) = await OpenAsync();

        editor.Tool = WorldMapTool.Region;
        editor.TraceSquare(3, 0, isDrag: false);
        editor.Tool = WorldMapTool.Erase;

        // Erasing a square takes out every map held there; the inside, map 9, is of another sub-area and is not held.
        editor.TraceSquare(2, 0, isDrag: false);

        Assert.Equal(5, editor.MapCount);
        Assert.Contains((2, 0, true), Squares(editor));
        Assert.Equal("5 maps · 1 sub-area · 1 left out", editor.Summary);

        editor.Tool = WorldMapTool.Trace;
        editor.TraceSquare(2, 0, isDrag: false);

        Assert.Equal(6, editor.MapCount);
        Assert.Contains((2, 0, false), Squares(editor));

        editor.Tool = WorldMapTool.Erase;
        editor.TraceSquare(4, 0, isDrag: false);
        editor.SubAreas.Single().RemoveCommand.Execute(null);

        Assert.True(editor.IsEmpty);
        Assert.Empty(editor.Scene.Squares!);
    }

    [Fact]
    public async Task ErasingAMapNamedTakesItOutAndErasingWhatIsNotInChangesNothing()
    {
        var (_, editor) = await OpenAsync(new ZoneAsset { Id = "z1", Name = "North", Maps = [1, 2] });

        editor.Tool = WorldMapTool.Erase;
        editor.TraceSquare(5, 0, isDrag: false);

        Assert.False(editor.CanUndo);

        editor.TraceSquare(1, 0, isDrag: false);

        Assert.Equal(1, editor.MapCount);
        Assert.True(editor.IsDirty);

        editor.Undo();

        Assert.Equal(2, editor.MapCount);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task SavingWritesTheZoneAscendingAndAnotherLayerShowsItsOwnSquares()
    {
        var (zones, editor) = await OpenAsync();
        var saved = new List<string>();

        editor.Add(5);
        editor.Add(1);
        editor.Add(7);
        editor.Add(123);

        Assert.Equal(3, editor.MapCount);

        await editor.SaveCommand.ExecuteAsync(null);
        saved.Add(string.Join(',', _store.Assets.Find<ZoneAsset>("z1")!.Maps));

        Assert.False(editor.IsDirty);
        Assert.Equal(["1,5,7"], saved);

        // Map 7 stands on world 2.
        Assert.Equal(1, editor.Layer);
        Assert.Equal([(1, 0, false), (5, 0, false)], Squares(editor));

        editor.UseLayer(2);

        Assert.Equal([(1, 1, false)], Squares(editor));
        Assert.Equal(zones.Open, [editor]);
    }

    [Fact]
    public async Task TheZoneShowsTheLayerItsMapsStandOnAndTakesTheFileAsItChangesUnlessItHoldsChanges()
    {
        var (zones, editor) = await OpenAsync(new ZoneAsset { Id = "z1", Name = "North", Maps = [7] });

        Assert.Equal(2, editor.Layer);

        await _store.Assets.SaveAsync(new ZoneAsset { Id = "z1", Name = "North", Maps = [7, 5] }, TestContext.Current.CancellationToken);
        zones.Refresh();

        Assert.Equal(2, editor.MapCount);
        Assert.False(editor.CanUndo);

        editor.UseLayer(1);
        editor.TraceSquare(1, 0, isDrag: false);
        await _store.Assets.SaveAsync(new ZoneAsset { Id = "z1", Name = "North", Maps = [7] }, TestContext.Current.CancellationToken);
        zones.Refresh();

        // What was done is kept, the file's changes not taken.
        Assert.Equal(3, editor.MapCount);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task ANewZoneIsNamedFreshOpenedInATabAndClosingItWithChangesAsksFirst()
    {
        await _store.OpenedAsync();

        var services = TestRouteWorld.Services(_store.Assets);
        var zones = new ZonesViewModel(_localizer, new Dialogs(), new Toasts(), () => services);
        var opened = new List<string>();
        var closed = new List<string>();

        zones.Opened = editor => opened.Add(editor.Name);
        zones.Closed = editor => closed.Add(editor.Name);

        Assert.True(zones.IsAvailable);

        var first = (await zones.CreateAsync(""))!;
        var second = (await zones.CreateAsync(""))!;

        Assert.Equal(["New zone", "New zone 2"], opened);
        Assert.Equal(AssetKind.Zone, _store.Assets.Find(first.Id)!.Kind);
        Assert.Equal([first, second], zones.Open);

        first.TraceSquare(1, 0, isDrag: false);

        Assert.True(zones.IsAnyDirty);

        first.CloseCommand.Execute(null);

        Assert.Equal(2, zones.Open.Count);
        Assert.NotNull(zones.Dialogs.Current);

        second.CloseCommand.Execute(null);

        Assert.Equal(["New zone 2"], closed);

        await first.SaveCommand.ExecuteAsync(null);

        Assert.Equal([1L], _store.Assets.Find<ZoneAsset>(first.Id)!.Maps);
        Assert.False(zones.IsAnyDirty);

        await _store.Assets.DeleteAsync(first.Id, TestContext.Current.CancellationToken);
        zones.Refresh();

        Assert.Empty(zones.Open);
    }

    [Fact]
    public async Task TheHudNamesTheSquareItsSubAreaAndWhetherTheZoneHoldsIt()
    {
        var (_, editor) = await OpenAsync(new ZoneAsset { Id = "z1", Name = "North", Maps = [1], Excluded = [2] });

        editor.Hover(new WorldMapHit(WorldMapHitKind.Square, 1, 0, -1, -1));

        Assert.Equal("1,0 · Sub-area 95 · in the zone", editor.Hud);

        editor.Hover(new WorldMapHit(WorldMapHitKind.Square, 2, 0, -1, -1));

        Assert.Contains("left out", editor.Hud, StringComparison.Ordinal);

        editor.Hover(new WorldMapHit(WorldMapHitKind.Square, 3, 0, -1, -1));

        Assert.EndsWith("outside the zone", editor.Hud, StringComparison.Ordinal);

        editor.Hover(WorldMapHit.None);

        Assert.Equal("Hover a map", editor.Hud);
    }
}
