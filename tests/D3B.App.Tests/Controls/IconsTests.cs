using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace D3B.App.Tests.Controls;

public sealed class IconsTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(IconsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryIntelliJIconReadsAsADrawingWithinItsGridOfSixteen()
    {
        // scripts/icons.cs writes the file: a shape Avalonia cannot read would fail only once a view draws it.
        var icons = await OnTheUIThreadAsync(() =>
        {
            var dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://D3B.App/Themes/IntelliJIcons.axaml"));

            return dictionary.Keys.OfType<string>().Select(key => (Key: key, Shapes: ((DrawingGroup)dictionary[key]!).Children.OfType<GeometryDrawing>().Select(shape => shape.Geometry!.Bounds).ToList())).ToList();
        });

        Assert.True(icons.Count >= 49);
        Assert.Contains(icons, icon => icon.Key == "Run.Run");
        Assert.All(icons, icon => Assert.Matches(@"^[A-Z][a-z]+\.[A-Z][A-Za-z]+$", icon.Key));
        Assert.All(icons, icon => Assert.NotEmpty(icon.Shapes));
        Assert.All(icons, icon => Assert.All(icon.Shapes, bounds => Assert.True(bounds is { Left: >= -0.5, Top: >= -0.5, Right: <= 16.5, Bottom: <= 16.5 } && bounds.Width + bounds.Height > 0, icon.Key)));
    }
}
