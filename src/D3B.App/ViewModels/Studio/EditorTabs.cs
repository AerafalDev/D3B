using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Studio;

/// <summary>The editor's tabs: each opened once, chosen when opened again, closed by its ×, its neighbour then shown (D320, D321).</summary>
internal sealed partial class EditorTabs : ObservableObject
{
    /// <summary>Gets the tabs, in the order they were opened.</summary>
    public ObservableCollection<EditorTab> Items { get; } = [];

    /// <summary>Gets the tab the editor shows, or <see langword="null"/> once every tab is closed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial EditorTab? Selected { get; private set; }

    /// <summary>Gets whether no tab is open, the editor showing the welcome's page alone.</summary>
    public bool IsEmpty => Selected is null;

    /// <summary>Shows a tab, opened if it is not.</summary>
    /// <param name="key">What the tab is.</param>
    /// <param name="create">Makes the tab, the first time.</param>
    /// <returns>The tab, now shown.</returns>
    public EditorTab Open(string key, Func<EditorTab> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        var tab = Items.FirstOrDefault(item => item.Key == key);

        if (tab is null)
        {
            tab = create();
            Items.Add(tab);
        }

        Select(tab);

        return tab;
    }

    /// <summary>Gets whether a tab is the one shown.</summary>
    /// <param name="key">What the tab is.</param>
    /// <returns>Whether it is shown.</returns>
    public bool IsShown(string key)
    {
        return Selected?.Key == key;
    }

    /// <summary>Says each tab's name again, in the language chosen.</summary>
    public void Refresh()
    {
        foreach (var tab in Items)
            tab.Refresh();
    }

    /// <summary>Shows a tab already open.</summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void Select(EditorTab? tab)
    {
        if (tab is not null && !Items.Contains(tab))
            return;

        foreach (var item in Items)
            item.IsSelected = item == tab;

        Selected = tab;
    }

    /// <summary>Closes a tab by its ×, unless what it shows keeps it to ask first.</summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void Close(EditorTab? tab)
    {
        if (tab?.Closing is { } closing && !closing())
            return;

        Drop(tab);
    }

    /// <summary>Closes a tab, whatever it shows; the one shown, its neighbour on the left shows, else on the right.</summary>
    /// <param name="tab">The tab.</param>
    public void Drop(EditorTab? tab)
    {
        var index = tab is null ? -1 : Items.IndexOf(tab);

        if (index < 0)
            return;

        Items.RemoveAt(index);

        if (Selected == tab)
            Select(Items.Count == 0 ? null : Items[Math.Max(0, index - 1)]);
    }

    /// <summary>Closes every tab.</summary>
    [RelayCommand]
    public void CloseAll()
    {
        Items.Clear();
        Select(null);
    }
}
