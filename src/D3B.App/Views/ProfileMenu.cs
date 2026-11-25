using Avalonia.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Properties;

namespace D3B.App.Views;

/// <summary>The menu of a character's profile, under its field in the Details and in its tab's header: open it, or give another (D345, D514).</summary>
internal static class ProfileMenu
{
    /// <summary>Opens the menu under a field.</summary>
    /// <param name="character">The character whose profile it gives.</param>
    /// <param name="field">The field it opens under.</param>
    public static void Open(CharacterPropertiesViewModel character, Control field)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(field);

        // Made as it opens, as the cards' menu: the profiles may have changed since.
        var texts = Localizer.Instance;
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft };

        if (character.HasProfile)
        {
            menu.Items.Add(Item(texts["OpenProfile"], () => character.OpenProfileCommand.Execute(null)));
            menu.Items.Add(new Separator());
        }

        void Group(string heading, IReadOnlyList<ProfileChoice> choices)
        {
            if (choices.Count == 0)
                return;

            menu.Items.Add(new MenuItem { Header = heading, Classes = { "Heading" } });

            foreach (var choice in choices)
                menu.Items.Add(new MenuItem { Header = choice.Name, Tag = choice.Detail, Command = character.AssignCommand, CommandParameter = choice });
        }

        Group(texts["ProfilesMine"], character.MyProfiles);
        Group(texts["ProfilesGiven"], character.GivenProfiles);
        Group(texts["ProfilesActivities"], character.ActivityProfiles);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["NoProfile"], () => character.AssignCommand.Execute(null)));
        field.ContextMenu = menu;
        menu.Closed += (_, _) => field.ContextMenu = null;
        menu.Open(field);
    }

    private static MenuItem Item(string text, Action run)
    {
        var item = new MenuItem { Header = text };

        item.Click += (_, _) => run();

        return item;
    }
}
