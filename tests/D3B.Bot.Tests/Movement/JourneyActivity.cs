using D3B.Bot.Activities;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Movement;

internal sealed class JourneyActivity : BotActivity
{
    private readonly long _mapId;
    private readonly int _teleportChanges;
    private readonly bool _usesTeleports;

    public override int TeleportChanges => _teleportChanges;

    public override bool UsesTeleports => _usesTeleports;

    public JourneyActivity(long mapId, int teleportChanges, bool usesTeleports = true)
    {
        _mapId = mapId;
        _teleportChanges = teleportChanges;
        _usesTeleports = usesTeleports;
    }

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await context.TravelAsync(new HashSet<long> { _mapId }, cancellationToken);

        return false;
    }
}
