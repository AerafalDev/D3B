using D3B.Bot.Activities;
using D3B.Bot.Behaviors;
using D3B.Bot.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Assets;

/// <summary>Reads the project before the host starts, with the behaviors and profiles given with the app (D296).</summary>
/// <remarks>A file that does not read is left out and logged: the project never stops the host, the profiles it holds do not start.</remarks>
internal sealed class AssetPreparation : IHostPreparation
{
    private readonly AssetLibrary _assets;
    private readonly ActivityCatalog _activities;
    private readonly ILogger<AssetPreparation> _log;

    public AssetPreparation(AssetLibrary assets, ActivityCatalog activities, ILogger<AssetPreparation> log)
    {
        _assets = assets;
        _activities = activities;
        _log = log;
    }

    public async Task<string?> PrepareAsync(CancellationToken cancellationToken)
    {
        await _assets.LoadAsync(GivenAssets.Of(_activities), cancellationToken).ConfigureAwait(false);

        foreach (var problem in _assets.Problems)
            BehaviorLog.AssetNotRead(_log, problem.Path, problem.Message);

        BehaviorLog.AssetsRead(_log, _assets.Assets.Count, _assets.Folder, _assets.Problems.Count);

        return null;
    }
}
