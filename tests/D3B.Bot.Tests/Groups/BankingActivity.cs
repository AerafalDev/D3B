using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Groups;

/// <summary>A master's profile that banks at a share of its pods, its loot alone, and whose steps wait a second each.</summary>
internal sealed class BankingActivity : BotActivity
{
    /// <summary>Gets or sets the share of pods the profile banks at.</summary>
    public int FullAt { get; init; } = 90;

    /// <summary>Gets or sets the items the profile banks of the master's own.</summary>
    public IReadOnlySet<int> Loot { get; init; } = new HashSet<int>();

    /// <summary>Gets or sets whether the profile banks all the character does not wear, its food too (D459).</summary>
    public bool All { get; init; }

    /// <summary>Gets the number of times the activity's own unloading was asked.</summary>
    public int OwnUnloads { get; private set; }

    public override ImmutableArray<ActivityWatch> Watches => [new ActivityWatch(WatchedGauge.Pods, FullAt, WatchAction.Bank)];

    public override bool DepositsAll => All;

    public override bool IsFull(CharacterState state)
    {
        return state.MaxWeight > 0 && state.Weight * 100L >= (long)FullAt * state.MaxWeight;
    }

    public override bool Deposits(InventoryItem stack)
    {
        return All || Loot.Contains(stack.ItemId);
    }

    public override Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        OwnUnloads++;

        return Task.FromResult(true);
    }

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        // A step that ends, so that the run asks again before the next whether a member is full, as with a real profile.
        await Task.Delay(TimeSpan.FromSeconds(1), context.Time, cancellationToken);

        return true;
    }
}
