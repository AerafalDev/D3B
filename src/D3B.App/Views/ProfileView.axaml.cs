using System.ComponentModel;
using Avalonia.Controls;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>A profile's tab, as an asset editor: its name and behavior, its settings in categories, the world's map beside to pick the place (D330, D415).</summary>
internal sealed partial class ProfileView : UserControl
{
    // Below it the labels go above their control, as the mockup's container query does.
    private const double NarrowForm = 560;

    // Below it the map leaves its column and lies above the form, as a band.
    private const double BandBelow = 640;

    private const double MapShare = 0.4;
    private const double LeastMap = 300;
    private const double MostMap = 460;
    private const double WideMapHeight = 280;
    private const double BandMapHeight = 150;

    private ProfileViewModel? _profile;
    private ProfileMapViewModel? _map;

    /// <summary>Initializes a new instance of the <see cref="ProfileView"/> class.</summary>
    public ProfileView()
    {
        InitializeComponent();
        Body.SizeChanged += (_, e) => Fit(e.NewSize.Width);
        Map.Clicked += hit => _map?.Clicked(hit);
        Map.HitChanged += () => _map?.Hover(Map.Hit);
        DataContextChanged += (_, _) => Follow();
    }

    // The map's column is a share of the tab, kept between two widths; a tab too narrow for it puts the map above the form.
    private void Fit(double width)
    {
        var band = width < BandBelow;
        var column = band ? 0 : Math.Clamp(width * MapShare, LeastMap, MostMap);

        Classes.Set("Band", band);
        WideHost.Width = band ? double.NaN : column;
        MapFrame.Height = band ? BandMapHeight : WideMapHeight;

        if (band && Side.Parent == WideHost)
        {
            WideHost.Child = null;
            BandHost.Child = Side;
        }
        else if (!band && Side.Parent == BandHost)
        {
            BandHost.Child = null;
            WideHost.Child = Side;
        }

        Classes.Set("Narrow", width - column < NarrowForm);
    }

    private void Follow()
    {
        if (_profile is not null)
            _profile.PropertyChanged -= OnProfileChanged;

        if (_map is not null)
            _map.FitRequested -= FitMap;

        _profile = DataContext as ProfileViewModel;
        _map = _profile?.Map;

        if (_profile is not null)
            _profile.PropertyChanged += OnProfileChanged;

        if (_map is not null)
            _map.FitRequested += FitMap;

        // A profile that plays a list of steps has no map to pick a place on.
        Classes.Set("Plays", _profile?.IsBehaviorProfile == true);
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileViewModel.IsBehaviorProfile))
            Classes.Set("Plays", _profile?.IsBehaviorProfile == true);
    }

    // Put on what the settings name; a map not arranged yet fits itself once it is.
    private void FitMap()
    {
        if (Map.Bounds.Width > 1 && Map.Bounds.Height > 1)
            Map.FitScene();
    }
}
