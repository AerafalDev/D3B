using Avalonia.Controls;

namespace MyPlugin;

/// <summary>The plugin's page in the app, in its rail after the app's own pages.</summary>
/// <remarks>
/// It inherits the app's theme, dark or light, its typeface and ShadUI's styles, and may use the app's classes:
/// <c>PageHeader</c>, <c>PageTitle</c>, <c>Plate</c> and <c>PlateFooter</c>, and the typography's, such as <c>Small</c>,
/// <c>Caption</c> and <c>Muted</c>.
/// </remarks>
public sealed partial class MyPluginPage : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="MyPluginPage"/> class.</summary>
    public MyPluginPage()
    {
        InitializeComponent();
    }

    /// <summary>Initializes a new instance of the <see cref="MyPluginPage"/> class, showing the page's model.</summary>
    /// <param name="model">The page's model.</param>
    internal MyPluginPage(MyPluginPageModel model)
        : this()
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Refresh();
        DataContext = model;
    }
}
