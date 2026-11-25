using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using D3B.App.Controls;

namespace D3B.App.Tests.Controls;

public sealed class StudioColorsTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StudioColorsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheDrawingCodesColoursAreThePalettesKeysSoThatAStateOrAKindHasOneColour()
    {
        var pairs = new (string Key, Color Drawn)[]
        {
            ("AccentColor", StudioColors.Accent),
            ("PlayingColor", StudioColors.Playing),
            ("WarningColor", StudioColors.Warning),
            ("ErrorColor", StudioColors.Error),
            ("RouteColor", StudioColors.Route),
            ("ZoneColor", StudioColors.Zone),
            ("BehaviorColor", StudioColors.Behavior),
            ("ProfileTintColor", StudioColors.Profile),
        };

        var palette = await OnTheUIThreadAsync(() => pairs.Select(pair => Application.Current!.TryGetResource(pair.Key, null, out var value) ? value : null).ToList());

        Assert.Equal(pairs.Select(pair => (object?)pair.Drawn), palette);
    }
}
