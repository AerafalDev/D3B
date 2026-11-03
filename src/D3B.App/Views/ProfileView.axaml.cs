using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D3B.App.ViewModels.Settings;

namespace D3B.App.Views;

/// <summary>A profile's tab: its name and behavior, its settings on a grid by section, its preview beside (D330).</summary>
internal sealed partial class ProfileView : UserControl
{
    // Below it the labels go above their control, as the mockup's container query does.
    private const double NarrowForm = 560;

    private const double SectionReach = 40;

    /// <summary>Initializes a new instance of the <see cref="ProfileView"/> class.</summary>
    public ProfileView()
    {
        InitializeComponent();
        Form.ScrollChanged += (_, _) => MarkSection();
        Body.SizeChanged += (_, e) => Fit(e.NewSize.Width);
    }

    private void Fit(double width)
    {
        Classes.Set("Narrow", width - Side.Width < NarrowForm);
    }

    private void OnSectionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SettingSectionViewModel section } || ContainerOf(section) is not { } container)
            return;

        var top = container.TranslatePoint(default, Sections);

        if (top is { } point)
            Form.Offset = new Vector(Form.Offset.X, point.Y);

        MarkSection();
    }

    private Control? ContainerOf(SettingSectionViewModel section)
    {
        return Sections.ContainerFromItem(section);
    }

    // The section whose title the form passed last is the one the bar lights, as the scroll moves.
    private void MarkSection()
    {
        SettingSectionViewModel? current = null;

        foreach (var item in Sections.Items.OfType<SettingSectionViewModel>().Where(section => section.IsVisible))
        {
            current ??= item;

            if (ContainerOf(item)?.TranslatePoint(default, Form) is { } point && point.Y <= SectionReach)
                current = item;
        }

        foreach (var button in SectionBar.GetVisualDescendants().OfType<Button>())
            button.Classes.Set("On", button.DataContext == current);
    }
}
