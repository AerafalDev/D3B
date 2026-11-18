using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Bot.Groups;

/// <summary>The group's life before an attack: the share of life the master's profile heals under, which each member's is over before the master attacks (D433).</summary>
/// <remarks>Pure functions of the bots, so that the master's gate and its followers read the same share.</remarks>
internal static class GroupLife
{
    // Tenths of a second a life point takes, when the server did not say: as measured in every capture (D153).
    private const int RegainPoll = 5;

    /// <summary>Gets the share of life, in percent, the master's profile heals under: the life its activity watches (D325).</summary>
    /// <param name="master">The master's bot.</param>
    /// <returns>The share; none when its activity heals not, or none runs.</returns>
    public static int? ShareOf(BotActor master)
    {
        ArgumentNullException.ThrowIfNull(master);

        if (master.Activity is not { IsOver: false } run)
            return null;

        foreach (var watch in run.Activity.Watches)
        {
            if (watch.Gauge is WatchedGauge.Life)
                return watch.Threshold;
        }

        return null;
    }

    /// <summary>Gets the life points a share of a most is, rounded up, as the fighting activity regains it (D198).</summary>
    /// <param name="share">The share, in percent.</param>
    /// <param name="most">The most life.</param>
    /// <returns>The life.</returns>
    public static long PointsOf(int share, int most)
    {
        return (((long)share * most) + 99) / 100;
    }

    /// <summary>Tells how long a member's life still takes to regenerate to a share, by the bot's reckoning (D153).</summary>
    /// <param name="member">The member's bot.</param>
    /// <param name="share">The share, in percent.</param>
    /// <param name="at">The time, by the bot's clock.</param>
    /// <returns>Zero when its life is over the share, or unknown; else the time its points take.</returns>
    public static TimeSpan Lacking(BotActor member, int share, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(member);

        var state = member.State;

        if (member.LifeAt(at) is not { } life || state.MaxLife is not { } most || most <= 0 || PointsOf(share, most) is var wanted && life >= wanted)
            return TimeSpan.Zero;

        var point = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));

        return TimeSpan.FromTicks(point.Ticks * (wanted - life));
    }
}
