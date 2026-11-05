using Avalonia.Controls;
using D3B.App.ViewModels.Settings;

namespace D3B.App.Views;

/// <summary>A settings editor's fields, by section, each typed and checked as the user types: a profile's, or an action's of a route (D98, D330, D377).</summary>
internal sealed partial class SettingsFormView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsFormView"/> class.</summary>
    public SettingsFormView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Fit();
    }

    /// <summary>Gets the sections the form shows.</summary>
    public IEnumerable<SettingSectionViewModel> Shown => Sections.Items.OfType<SettingSectionViewModel>();

    /// <summary>Finds the control that shows a section.</summary>
    /// <param name="section">The section.</param>
    /// <returns>Its control, or <see langword="null"/> while it is not built.</returns>
    public Control? ContainerOf(SettingSectionViewModel section)
    {
        return Sections.ContainerFromItem(section);
    }

    // A field alone, as a token's popover shows it, or the editor's sections whole.
    private void Fit()
    {
        var field = DataContext as SettingFieldViewModel;

        Single.Content = field;
        Single.IsVisible = field is not null;
        Sections.IsVisible = field is null;
    }
}
