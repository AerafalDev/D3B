using D3B.Bot.Activities;
using D3B.Bot.Fights;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Fights;

/// <summary>An activity that fights its own fights with a choice of challenges' bonus, each step waiting until it is cut.</summary>
internal sealed class RewardActivity : BotActivity
{
    /// <summary>Gets or sets the choice the profile makes.</summary>
    public ChallengeReward Reward { get; init; }

    /// <summary>Gets or sets whether the AI plays the activity's fights, as a harvest's profile says.</summary>
    public bool Fights { get; init; } = true;

    /// <summary>Gets or sets which challenges the AI plays for.</summary>
    public ChallengePlay Play { get; init; }

    /// <summary>Gets or sets the families of the wild mounts the profile captures (D533).</summary>
    public IReadOnlyList<int> Races { get; init; } = [];

    /// <summary>Gets or sets whether the profile captures the archmonsters' souls (D565).</summary>
    public bool Souls { get; init; }

    /// <summary>Gets or sets whether the character fights as a mule (D579).</summary>
    public bool Mule { get; init; }

    public override bool FightsItsFights => Fights;

    public override ChallengeReward ChallengeReward => Reward;

    public override ChallengePlay Challenges => Play;

    public override IReadOnlyList<int> MountRaces => Races;

    public override bool CapturesSouls => Souls;

    public override bool FightsAsMule => Mule;

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);

        return true;
    }
}
