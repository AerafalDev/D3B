using Avalonia.Controls;

namespace D3B.Samples.Hello;

/// <summary>The plugin's page in the app: a card for each bot, with what its strolls did.</summary>
/// <remarks>
/// <para>
/// It uses the app's classes, <c>PageHeader</c>, <c>PageTitle</c>, <c>Plate</c> and the typography's, so that it looks like
/// the app's own pages.
/// </para>
/// <para>
/// It is public, as a page may be: a host without the app, which lacks Avalonia, reads the plugin's root module from its
/// metadata and never loads this type (D97).
/// </para>
/// </remarks>
public sealed partial class HelloPage : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="HelloPage"/> class.</summary>
    public HelloPage()
    {
        InitializeComponent();
    }

    /// <summary>Initializes a new instance of the <see cref="HelloPage"/> class, showing a page's model.</summary>
    /// <param name="model">The page's model.</param>
    internal HelloPage(HelloPageModel model)
        : this()
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Refresh();
        DataContext = model;
    }
}
