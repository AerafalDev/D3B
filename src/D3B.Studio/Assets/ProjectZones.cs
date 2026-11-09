using D3B.Bot.Activities;
using D3B.Data.World;

namespace D3B.Studio.Assets;

/// <summary>The project's zones, which the activities whose place is one read (D415).</summary>
/// <remarks>
/// A zone's maps are reckoned from the atlas, which holds tens of thousands of positions, so they are kept until the project changes
/// (<see cref="AssetLibrary.Version"/>). The zone's file is read by the library, not here: a zone saved while a bot plays is seen at
/// the activity's next start.
/// </remarks>
public sealed class ProjectZones : IPlaceZones
{
    private readonly AssetLibrary _library;
    private readonly Func<WorldAtlas> _atlas;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, IReadOnlySet<long>> _kept = [];
    private int _version = -1;

    /// <summary>Initializes a new instance of the <see cref="ProjectZones"/> class.</summary>
    /// <param name="library">The project.</param>
    /// <param name="atlas">Gets the atlas, which says each map's sub-area; read only when a zone is asked for.</param>
    public ProjectZones(AssetLibrary library, Func<WorldAtlas> atlas)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(atlas);

        _library = library;
        _atlas = atlas;
    }

    /// <inheritdoc/>
    public bool TryResolve(string zone, out IReadOnlySet<long> maps)
    {
        ArgumentNullException.ThrowIfNull(zone);

        lock (_lock)
        {
            if (_version != _library.Version)
            {
                _kept.Clear();
                _version = _library.Version;
            }

            if (_kept.TryGetValue(zone, out var known))
            {
                maps = known;

                return true;
            }

            if (_library.Find<ZoneAsset>(zone) is not { } asset)
            {
                maps = new HashSet<long>();

                return false;
            }

            maps = _kept[zone] = ZoneMaps.Resolve(asset, _atlas());

            return true;
        }
    }
}
