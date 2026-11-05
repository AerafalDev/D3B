using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.App.ViewModels.Routes;
using D3B.Bot.Assets;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Properties;

/// <summary>An asset in the inspector: its name and kind, then its file, what it uses and what uses it (D324).</summary>
internal sealed partial class AssetPropertiesViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private AssetLibrary? _assets;
    private AccountBook? _book;
    private string _filter = "";

    /// <summary>Gets the asset shown, by its id.</summary>
    [ObservableProperty]
    public partial string? Id { get; private set; }

    /// <summary>Gets its name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets its kind, and whether it is given with D3B.</summary>
    [ObservableProperty]
    public partial string KindText { get; private set; } = "";

    /// <summary>Gets its kind.</summary>
    [ObservableProperty]
    public partial AssetKind Kind { get; private set; }

    /// <summary>Gets its file, relative to the project's folder, or that it is built in.</summary>
    [ObservableProperty]
    public partial string File { get; private set; } = "";

    /// <summary>Gets the format its file is written in.</summary>
    [ObservableProperty]
    public partial string Format { get; private set; } = "";

    /// <summary>Gets what it uses: a profile's behavior and how many values it changes.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetUseViewModel> Uses { get; private set; } = [];

    /// <summary>Gets what uses it: the assets, and the characters that play a profile.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetUseViewModel> UsedBy { get; private set; } = [];

    /// <summary>Gets whether the search shows the file's section.</summary>
    [ObservableProperty]
    public partial bool ShowsFile { get; private set; } = true;

    /// <summary>Gets whether the search shows what it uses.</summary>
    [ObservableProperty]
    public partial bool ShowsUses { get; private set; } = true;

    /// <summary>Gets whether the search shows what uses it.</summary>
    [ObservableProperty]
    public partial bool ShowsUsedBy { get; private set; } = true;

    /// <summary>Gets the editor of the route shown, once it is open: its parameters and steps are the inspector's (D371).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoute))]
    public partial RouteEditorViewModel? Route { get; private set; }

    /// <summary>Gets or sets what finds a route's open editor by its id.</summary>
    public Func<string, RouteEditorViewModel?>? RouteOf { get; set; }

    /// <summary>Gets whether the asset shown is a route being edited.</summary>
    public bool HasRoute => Route is not null;

    /// <summary>Gets whether the asset uses another.</summary>
    public bool HasUses => Uses.Count > 0;

    /// <summary>Gets whether nothing uses the asset.</summary>
    public bool IsUnused => UsedBy.Count == 0;

    /// <summary>Initializes a new instance of the <see cref="AssetPropertiesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    public AssetPropertiesViewModel(Localizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Shows an asset.</summary>
    /// <param name="id">The asset's id.</param>
    public void Show(string id)
    {
        Id = id;
    }

    /// <summary>Reads the asset again, in the language chosen.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _assets is null)
        {
            _assets = services.GetService<AssetLibrary>();
            _book = services.GetService<AccountBook>();
        }

        if (Id is not { } id || _assets?.Find(id) is not { } asset)
            return;

        Name = asset.Title?.In(_localizer.Culture) ?? asset.Name;
        Kind = asset.Kind;
        KindText = asset.IsGiven ? _localizer.Format("AssetGivenKind", _localizer["AssetKind" + asset.Kind]) : _localizer["AssetKind" + asset.Kind];
        File = asset.Path ?? _localizer["AssetBuiltIn"];
        Format = AssetFiles.Format.ToString(_localizer.Culture);
        Route = asset.Kind == AssetKind.Route ? RouteOf?.Invoke(id) : null;
        Uses = [.. asset.Uses().Select(used => _assets.Find(used)).OfType<Asset>().Select(used => Use(used, asset))];
        UsedBy =
        [
            .. _assets.UsedBy(id).Select(user => new AssetUseViewModel(user.Title?.In(_localizer.Culture) ?? user.Name, _localizer["AssetKind" + user.Kind], user.Kind, null)),
            .. (_book?.Characters ?? []).Where(character => character.Profile == id).Select(character => new AssetUseViewModel(character.Name, character.StartsOnConnection ? _localizer["UsedOnConnection"] : "", null, character.Key)),
        ];
        OnPropertyChanged(nameof(HasUses));
        OnPropertyChanged(nameof(IsUnused));
        ApplyFilter(_filter);
    }

    /// <summary>Shows only the sections whose label or value holds a text, as the inspector's search asks.</summary>
    /// <param name="filter">The text, or empty for every section.</param>
    public void ApplyFilter(string filter)
    {
        _filter = filter.Trim();

        bool Has(params string[] texts)
        {
            return _filter.Length == 0 || texts.Any(text => text.Contains(_filter, StringComparison.CurrentCultureIgnoreCase));
        }

        ShowsFile = Has(_localizer["PropertyName"], Name, _localizer["PropertyFile"], File, _localizer["PropertyFormat"]);
        ShowsUses = HasUses && Has([_localizer["AssetUses"], .. Uses.Select(use => use.Name)]);
        ShowsUsedBy = Has([_localizer["AssetUsedBy"], .. UsedBy.Select(use => use.Name)]);
    }

    private AssetUseViewModel Use(Asset used, Asset asset)
    {
        var detail = asset is ProfileAsset { Values.Count: > 0 } profile ? _localizer.Plural("ValuesChanged", profile.Values.Count)
            : used.IsGiven ? _localizer["AssetGiven"]
            : _localizer["AssetKind" + used.Kind];

        return new AssetUseViewModel(used.Title?.In(_localizer.Culture) ?? used.Name, detail, used.Kind, null);
    }
}
