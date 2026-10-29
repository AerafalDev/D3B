using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace D3B.App.ViewModels.Settings;

/// <summary>A value a setting offers: a choice's, or one of the game's, named in the app's language.</summary>
internal sealed class OptionViewModel
{
    /// <summary>Gets what a search keeps: the names that hold the text typed, whatever its case and accents, as "frene" finds Frêne.</summary>
    public static AutoCompleteFilterPredicate<object?> Filter { get; } = (search, item) => item is OptionViewModel option
        && (string.IsNullOrWhiteSpace(search)
            || CultureInfo.InvariantCulture.CompareInfo.IndexOf(option.Name, search.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>Gets the value the setting stores: a value of an enumeration, or an id of the game's.</summary>
    public object Value { get; }

    /// <summary>Gets its name.</summary>
    public string Name { get; }

    /// <summary>Gets what the app says beside its name, such as its level.</summary>
    public string? Detail { get; }

    /// <summary>Gets whether it has a detail.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary>Gets its icon, such as a resource's or a profession's, when the pack keeps one (D135).</summary>
    public IImage? Icon { get; }

    /// <summary>Gets the icon of what its detail names, such as a resource's profession.</summary>
    public IImage? DetailIcon { get; }

    /// <summary>Initializes a new instance of the <see cref="OptionViewModel"/> class.</summary>
    /// <param name="value">The value the setting stores.</param>
    /// <param name="name">Its name.</param>
    /// <param name="detail">What the app says beside its name.</param>
    /// <param name="icon">Its icon.</param>
    /// <param name="detailIcon">The icon of what its detail names.</param>
    public OptionViewModel(object value, string name, string? detail = null, IImage? icon = null, IImage? detailIcon = null)
    {
        Value = value;
        Name = name;
        Detail = detail;
        Icon = icon;
        DetailIcon = detailIcon;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        // The text a search box shows once the option is chosen.
        return Name;
    }
}
