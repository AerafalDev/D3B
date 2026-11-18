using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Groups;

/// <summary>A master's profile that heals under a share of its life and acts at its first step, an attack by default, then waits.</summary>
internal sealed class AttackingActivity : BotActivity
{
    public const int GroupId = -5;

    /// <summary>Gets or sets the share of life the profile heals under; none, the profile heals not.</summary>
    public int? HealBelow { get; init; } = 70;

    /// <summary>Gets or sets the request its first step sends.</summary>
    public IMessage Request { get; init; } = new AttackMonsterRequest { MonsterGroupId = GroupId };

    public override ImmutableArray<ActivityWatch> Watches => HealBelow is { } share ? [new ActivityWatch(WatchedGauge.Life, share, WatchAction.Heal)] : [];

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await context.SendAsync(Request, cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, context.Time, cancellationToken);

        return false;
    }
}
