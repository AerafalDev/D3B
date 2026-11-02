using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Characters;

namespace D3B.App.Views;

/// <summary>The characters' panel: a card per character online, those offline folded under their heading (D323).</summary>
/// <remarks>
/// A click chooses, a double click or Enter opens the character's tab, and the right click chooses it, then opens its menu,
/// made as it opens: what it offers follows the character's state, which a binding inside a popup would not reach.
/// </remarks>
internal sealed partial class CharactersView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="CharactersView"/> class.</summary>
    public CharactersView()
    {
        InitializeComponent();
    }

    private static CharacterKey? KeyOf(object? item)
    {
        return item switch
        {
            BotViewModel bot => bot.Bot.Key,
            CharacterLineViewModel line => line.Key,
            _ => null,
        };
    }

    private static MenuItem Item(string text, string? keys, Action run, bool enabled = true)
    {
        var item = new MenuItem { Header = text, Tag = keys, IsEnabled = enabled };

        item.Click += (_, _) => run();

        return item;
    }

    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        Choose(sender);
    }

    private void OnLineTapped(object? sender, TappedEventArgs e)
    {
        Choose(sender);
    }

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        Open(sender);
    }

    private void OnLineDoubleTapped(object? sender, TappedEventArgs e)
    {
        Open(sender);
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        OnKey(sender, e);
    }

    private void OnLineKeyDown(object? sender, KeyEventArgs e)
    {
        OnKey(sender, e);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            Open(sender);
            e.Handled = true;
        }
        else if (e.Key is Key.Space)
        {
            Choose(sender);
            e.Handled = true;
        }
    }

    private void Choose(object? sender)
    {
        if (DataContext is CharactersViewModel panel && sender is Control { DataContext: var item } control && KeyOf(item) is { } key)
        {
            panel.Choose(key);
            control.Focus();
        }
    }

    private void Open(object? sender)
    {
        if (DataContext is CharactersViewModel panel && sender is Control { DataContext: var item } && KeyOf(item) is { } key)
            panel.Open(key);
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not CharactersViewModel panel || sender is not Control target)
            return;

        Choose(sender);
        e.Handled = true;

        var menu = new ContextMenu();
        var texts = Localizer.Instance;
        var run = panel.RunBlock;

        menu.Items.Add(Item(texts["MenuOpen"], texts["KeysEnter"], () => panel.OpenChosenCommand.Execute(null)));

        if (panel.ChosenIsOnline && run is not null)
        {
            menu.Items.Add(new Separator());

            if (run.ShowsPause)
                menu.Items.Add(Item(texts["CommandPause"], "F6", run.Pause));
            else
                menu.Items.Add(Item(texts[run.IsPaused ? "MenuResume" : "CommandPlay"], "F5", run.Play, run.CanPlay));

            if (run.ShowsStop)
                menu.Items.Add(Item(texts["CommandStop"], texts["KeysShiftF5"], run.Stop));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Profiles(panel, texts));

        var starts = Item(texts["MenuStartOnConnection"], null, () => panel.ToggleStartsOnConnectionCommand.Execute(null), panel.ChosenHasProfile);

        starts.ToggleType = MenuItemToggleType.CheckBox;
        starts.IsChecked = panel.ChosenStartsOnConnection;
        menu.Items.Add(starts);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["ForgetAccount"], null, () => panel.ForgetCommand.Execute(null)));
        target.ContextMenu = menu;
        menu.Closed += OnMenuClosed;
        menu.Open(target);
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        // Made again at each right click: one left on its card would open as it was, its state since changed.
        if (sender is ContextMenu { Parent: Control owner } menu)
        {
            menu.Closed -= OnMenuClosed;
            owner.ContextMenu = null;
        }
    }

    private static MenuItem Profiles(CharactersViewModel panel, Localizer texts)
    {
        var profiles = new MenuItem { Header = texts["MenuProfile"] };

        void Group(string heading, IReadOnlyList<ProfileChoice> choices)
        {
            if (choices.Count == 0)
                return;

            profiles.Items.Add(new MenuItem { Header = heading, Classes = { "Heading" } });

            foreach (var choice in choices)
                profiles.Items.Add(Item(choice.Name, null, () => panel.AssignCommand.Execute(choice)));
        }

        Group(texts["ProfilesMine"], panel.MyProfiles);
        Group(texts["ProfilesGiven"], panel.GivenProfiles);
        Group(texts["ProfilesActivities"], panel.ActivityProfiles);
        profiles.Items.Add(new Separator());
        profiles.Items.Add(Item(texts["NoProfile"], null, () => panel.AssignCommand.Execute(null)));

        return profiles;
    }
}
