using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Properties;

/// <summary>The inspector: what is chosen, a character or an asset, its properties in sections, and a search that filters them (D324).</summary>
internal sealed partial class PropertiesViewModel : ObservableObject
{
    /// <summary>The labels' column at first, a share of the inspector's width, as the mockup's.</summary>
    public const double DefaultLabelShare = 0.36;

    /// <summary>Gets the character the inspector shows, when it shows one.</summary>
    public CharacterPropertiesViewModel Character { get; }

    /// <summary>Gets the asset the inspector shows, when it shows one.</summary>
    public AssetPropertiesViewModel Asset { get; }

    /// <summary>Gets whether the inspector shows a character.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool ShowsCharacter { get; private set; }

    /// <summary>Gets whether the inspector shows an asset.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool ShowsAsset { get; private set; }

    /// <summary>Gets or sets the text the properties are filtered by.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets or sets the labels' column, a share of the inspector's width, which its edge resizes.</summary>
    [ObservableProperty]
    public partial double LabelShare { get; set; } = DefaultLabelShare;

    /// <summary>Gets whether nothing is chosen, so that the inspector says what to choose.</summary>
    public bool IsEmpty => !ShowsCharacter && !ShowsAsset;

    /// <summary>Initializes a new instance of the <see cref="PropertiesViewModel"/> class.</summary>
    /// <param name="character">The character's properties.</param>
    /// <param name="asset">The asset's properties.</param>
    public PropertiesViewModel(CharacterPropertiesViewModel character, AssetPropertiesViewModel asset)
    {
        Character = character;
        Asset = asset;
    }

    /// <summary>Shows a character, or nothing.</summary>
    /// <param name="key">The character, or <see langword="null"/> for nothing.</param>
    public void ShowCharacter(CharacterKey? key)
    {
        if (key is { } shown)
            Character.Show(shown);

        ShowsCharacter = key is not null;
        ShowsAsset = false;
    }

    /// <summary>Shows an asset.</summary>
    /// <param name="id">The asset's id.</param>
    public void ShowAsset(string id)
    {
        Asset.Show(id);
        ShowsAsset = true;
        ShowsCharacter = false;
    }

    /// <summary>Reads what the inspector shows again.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (ShowsCharacter)
            Character.Refresh(services);

        if (ShowsAsset)
            Asset.Refresh(services);
    }

    partial void OnFilterChanged(string value)
    {
        Character.ApplyFilter(value);
        Asset.ApplyFilter(value);
    }

    partial void OnLabelShareChanged(double value)
    {
        // A label never takes less than a quarter, nor more than seven tenths: neither column vanishes.
        var bounded = Math.Clamp(value, 0.25, 0.7);

        if (bounded != value)
            LabelShare = bounded;
    }
}
