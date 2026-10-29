namespace D3B.Bot.Harvesting;

/// <summary>The harvesting activity's settings: what it harvests, where, and when it banks (D127).</summary>
public sealed class HarvestSettings
{
    /// <summary>Gets or sets the resources to harvest, by their items' ids, of any profession, the first harvested first on each map.</summary>
    public IReadOnlyList<int> Resources { get; set; } = [];

    /// <summary>Gets or sets whether to harvest every resource the character can at its professions' levels, rather than <see cref="Resources"/>.</summary>
    public bool EverythingKnown { get; set; }

    /// <summary>Gets or sets the area to harvest in: alone, its sub-areas where the resources grow.</summary>
    public int? Area { get; set; }

    /// <summary>Gets or sets the sub-areas to harvest in, of any area when no area is chosen.</summary>
    public IReadOnlyList<int> SubAreas { get; set; } = [];

    /// <summary>Gets or sets how full the character is, in percent of its pods, once it banks.</summary>
    public int FullAt { get; set; } = 90;

    /// <summary>Gets or sets what goes to the bank.</summary>
    public HarvestDeposit Deposit { get; set; } = HarvestDeposit.Harvest;
}
