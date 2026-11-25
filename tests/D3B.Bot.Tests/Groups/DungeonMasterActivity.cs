using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Groups;

/// <summary>A master's profile that says which dungeon of the table the character is in, and whose steps wait until they are cut.</summary>
internal sealed class DungeonMasterActivity : BotActivity
{
    /// <summary>Gets or sets the dungeon the master is in, if any.</summary>
    public int? Inside { get; set; }

    public override int? DungeonInside => Inside;

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, context.Time, cancellationToken);

        return true;
    }
}
