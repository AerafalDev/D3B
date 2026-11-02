using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.Studio;

/// <summary>A tab of the editor: the welcome, a page the studio keeps until its session draws it again, or a module's page (D321, D333).</summary>
internal sealed partial class EditorTab : ObservableObject
{
    private readonly Func<string> _title;

    /// <summary>Gets what the tab is, the same each time it opens: <c>welcome</c>, <c>accounts</c>, a module page's route.</summary>
    public string Key { get; }

    /// <summary>Gets what the tab shows, a view model the window draws by its type.</summary>
    public object Content { get; }

    /// <summary>Gets the tab's IntelliJ icon by its key, such as <c>Nodes.Folder</c>, or <see langword="null"/> for D3B's logo.</summary>
    public string? Icon { get; }

    /// <summary>Gets whether the tab shows nothing until the host runs, its content read from the host's services.</summary>
    public bool NeedsHost { get; }

    /// <summary>Gets the tab's name, in the app's language.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; }

    /// <summary>Gets or sets whether the tab is the one the editor shows.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="EditorTab"/> class.</summary>
    /// <param name="key">What the tab is.</param>
    /// <param name="title">Says the tab's name, in the app's language as it is then.</param>
    /// <param name="icon">The tab's IntelliJ icon by its key, or <see langword="null"/> for D3B's logo.</param>
    /// <param name="content">What the tab shows.</param>
    /// <param name="needsHost">Whether it shows nothing until the host runs.</param>
    public EditorTab(string key, Func<string> title, string? icon, object content, bool needsHost)
    {
        _title = title;
        Key = key;
        Icon = icon;
        Content = content;
        NeedsHost = needsHost;
        Title = title();
    }

    /// <summary>Says the tab's name again, in the language chosen.</summary>
    public void Refresh()
    {
        Title = _title();
    }
}
