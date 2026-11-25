using System.Collections;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Properties;

namespace D3B.App.Views;

/// <summary>The menu of a character's profile, under its field in the Details and in its tab's header: open it, or give another (D345, D514).</summary>
/// <remarks>
/// The field's own flyout, as a select: a click opens it, the next closes it, and so does a click elsewhere or the focus leaving
/// (D548). Its items are made as it opens, the profiles having perhaps changed since.
/// </remarks>
internal static class ProfileMenu
{
    /// <summary>Gives a field the menu of the character it shows.</summary>
    /// <param name="field">The field.</param>
    /// <param name="character">Gives the character the field shows, as it is when the menu opens.</param>
    public static void Attach(Button field, Func<CharacterPropertiesViewModel?> character)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(character);

        // Its own collection from the start: Avalonia 12's presenter, made over a flyout's still empty Items, never shows the
        // items added to them as it opens, and the menu opened empty.
        var items = new ObservableCollection<object>();
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft, ItemsSource = items };

        menu.Opening += (_, _) =>
        {
            items.Clear();

            if (character() is { } shown)
                Fill(items, shown);
        };
        field.Flyout = menu;
    }

    /// <summary>Puts the menu's items: open the profile, the project's profiles, the given ones, the activities, and none.</summary>
    /// <param name="items">The menu's items.</param>
    /// <param name="character">The character whose profile it gives.</param>
    internal static void Fill(IList items, CharacterPropertiesViewModel character)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(character);

        var texts = Localizer.Instance;

        if (character.HasProfile)
        {
            items.Add(Item(texts["OpenProfile"], () => character.OpenProfileCommand.Execute(null)));
            items.Add(new Separator());
        }

        void Group(string heading, IReadOnlyList<ProfileChoice> choices)
        {
            if (choices.Count == 0)
                return;

            items.Add(new MenuItem { Header = heading, Classes = { "Heading" } });

            foreach (var choice in choices)
                items.Add(new MenuItem { Header = choice.Name, Tag = choice.Detail, Command = character.AssignCommand, CommandParameter = choice });
        }

        Group(texts["ProfilesMine"], character.MyProfiles);
        Group(texts["ProfilesGiven"], character.GivenProfiles);
        Group(texts["ProfilesActivities"], character.ActivityProfiles);
        items.Add(new Separator());
        items.Add(Item(texts["NoProfile"], () => character.AssignCommand.Execute(null)));
    }

    private static MenuItem Item(string text, Action run)
    {
        var item = new MenuItem { Header = text };

        item.Click += (_, _) => run();

        return item;
    }
}
