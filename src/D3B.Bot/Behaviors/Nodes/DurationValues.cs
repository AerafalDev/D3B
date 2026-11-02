namespace D3B.Bot.Behaviors.Nodes;

/// <summary>A duration.</summary>
public sealed class DurationValues
{
    /// <summary>Gets or sets the minutes, for a run's length.</summary>
    public int Minutes { get; set; } = 60;

    /// <summary>Gets or sets the seconds, for a wait.</summary>
    public int Seconds { get; set; } = 5;
}
