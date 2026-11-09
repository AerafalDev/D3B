using D3B.App.Localization;
using D3B.App.ViewModels;

namespace D3B.App.Tests.ViewModels;

public sealed class RouteInspectorTests
{
    [Fact]
    public async Task ARouteIsInTheInspectorAsSoonAsItIsMadeAndItsStepsFollowWhatIsDoneOnTheMap()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var main = new MainViewModel(runner, new Localizer(Localizer.English));

        main.Refresh();
        main.Routes.Use(TestRouteWorld.Services(store.Assets));

        var editor = await main.Routes.CreateAsync("");

        Assert.NotNull(editor);
        Assert.True(main.Properties.ShowsAsset);
        Assert.Same(editor, main.Properties.Asset.Route);

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: false);
        await editor.Planned;

        Assert.Equal(2, main.Properties.Asset.Route!.Steps.Count);
    }

    [Fact]
    public async Task SelectingARouteAgainShowsItInTheDetailsInPlaceOfWhatWasThere()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var main = new MainViewModel(runner, new Localizer(Localizer.English));

        main.Refresh();
        main.Routes.Use(TestRouteWorld.Services(store.Assets));

        var first = await main.Routes.CreateAsync("");
        var second = await main.Routes.CreateAsync("");

        Assert.Same(second, main.Properties.Asset.Route);

        main.Selection.SelectRoute(first!.Id);

        Assert.Same(first, main.Properties.Asset.Route);
        Assert.Equal(first!.Id, main.Properties.Asset.Id);
    }

    [Fact]
    public async Task TheNameTypedInTheInspectorRenamesTheRouteAndItsFileAndABadOneIsLeftAsItWas()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var main = new MainViewModel(runner, new Localizer(Localizer.English));

        main.Refresh();
        main.Routes.Use(TestRouteWorld.Services(store.Assets));

        var editor = await main.Routes.CreateAsync("");
        var inspector = main.Properties.Asset;

        Assert.True(inspector.CanRename);

        inspector.NameText = "North loop";
        await inspector.CommitNameAsync();

        var renamed = store.Assets.Find(editor!.Id)!;

        Assert.Equal("North loop", renamed.Name);
        Assert.EndsWith("North loop.route.json", renamed.Path);

        inspector.NameText = "a/b";
        await inspector.CommitNameAsync();

        Assert.Equal("North loop", store.Assets.Find(editor.Id)!.Name);
        Assert.Equal("North loop", inspector.NameText);
    }
}
