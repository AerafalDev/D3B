using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Studio.Tests.Behaviors;

/// <summary>An activity that says something of itself in every way an activity can, its step waiting until cancelled.</summary>
internal sealed class SpeakingActivity : BotActivity
{
    public const int Dungeon = 5;
    public const int Race = 7;

    public static readonly DateTimeOffset Until = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override IReadOnlyList<int> MountRaces => [Race];

    public override bool CapturesSouls => true;

    public override bool FightsAsMule => true;

    public override bool RestartsAfterFailure => true;

    public override DateTimeOffset? WaitingUntil => Until;

    public override int? DungeonInside => Dungeon;

    public override bool BanksTheGroup => true;

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        return true;
    }

    public override bool Keeps(InventoryItem stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        return true;
    }
}
