using D3B.Bot.Activities;
using D3B.Bot.Fights;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Fights;

/// <summary>An activity that fights its own fights with a choice of challenges' bonus, each step waiting until it is cut.</summary>
internal sealed class RewardActivity : BotActivity
{
    /// <summary>Gets or sets the choice the profile makes.</summary>
    public ChallengeReward Reward { get; init; }

    public override bool FightsItsFights => true;

    public override ChallengeReward ChallengeReward => Reward;

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);

        return true;
    }
}
