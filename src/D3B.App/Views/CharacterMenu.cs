using Avalonia.Controls;
using Avalonia.Interactivity;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;

namespace D3B.App.Views;

/// <summary>The menu of a character's row: what it offers follows the character's state, made as it opens, which a binding inside a popup would not reach (D323, D397).</summary>
internal static class CharacterMenu
{
    /// <summary>Opens the menu of the character chosen, over a control.</summary>
    /// <param name="target">The control the menu opens at.</param>
    /// <param name="panel">The characters' model, whose chosen character the menu is of.</param>
    public static void Open(Control target, CharactersViewModel panel)
    {
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

    private static MenuItem Item(string text, string? keys, Action run, bool enabled = true)
    {
        var item = new MenuItem { Header = text, Tag = keys, IsEnabled = enabled };

        item.Click += (_, _) => run();

        return item;
    }

    private static void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        // Made again at each right click: one left on its row would open as it was, its state since changed.
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
