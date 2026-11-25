using D3B.Bot.Fights;

namespace D3B.Bot.Fighting;

/// <summary>The fighting activity's settings: which groups it attacks, where, its upkeep and its bank (D236).</summary>
/// <remarks>How the character fights is the AI's alone, the same for every character, as it judges each fight (D244).</remarks>
public sealed class FightSettings
{
    /// <summary>Gets or sets which groups to attack: every one the settings allow, or those holding a monster chosen.</summary>
    public FightMode Mode { get; set; } = FightMode.Automatic;

    /// <summary>Gets or sets the monsters a group is attacked for, by id, when they are chosen.</summary>
    public IReadOnlyList<int> Monsters { get; set; } = [];

    /// <summary>Gets or sets the families a group is attacked for a monster of, by their race's id, when they are chosen (D247).</summary>
    public IReadOnlyList<int> Families { get; set; } = [];

    /// <summary>Gets or sets the monsters a group is never attacked with, by id.</summary>
    public IReadOnlyList<int> Excluded { get; set; } = [];

    /// <summary>Gets or sets the least monsters a group attacked holds.</summary>
    public int LeastMonsters { get; set; } = 1;

    /// <summary>Gets or sets the most monsters a group attacked holds.</summary>
    public int MostMonsters { get; set; } = 8;

    /// <summary>Gets or sets how the place is chosen.</summary>
    public FightPlace Place { get; set; } = FightPlace.Automatic;

    /// <summary>Gets or sets how many sub-areas the activity hunts in turn when it chooses the place, drawn from the richest ten; 1 stays in the richest (D391).</summary>
    public int Rotation { get; set; } = 5;

    /// <summary>Gets or sets the zone of the project to hunt in, by its id, when <see cref="Place"/> is <see cref="FightPlace.Zone"/> (D415).</summary>
    public string Zone { get; set; } = "";

    /// <summary>Gets or sets the area to hunt in: alone, its sub-areas where monsters live.</summary>
    public int? Area { get; set; }

    /// <summary>Gets or sets the sub-areas to hunt in, of any area when no area is chosen.</summary>
    public IReadOnlyList<int> SubAreas { get; set; } = [];

    /// <summary>Gets or sets whether a tour goes through the isolated maps too, last, rather than leave them out (D149, D245).</summary>
    public bool IsolatedMaps { get; set; }

    /// <summary>Gets or sets how many map changes a zaap must save to be taken, when <see cref="UseZaaps"/> is on (D167).</summary>
    public int ZaapSaving { get; set; } = 5;

    /// <summary>Gets or sets whether to heal under a share of life before attacking (D148).</summary>
    public bool Heal { get; set; } = true;

    /// <summary>Gets or sets the share of its life under which the character heals, in percent: eating until its life is whole, or regenerating back to the share.</summary>
    public int HealBelow { get; set; } = 70;

    /// <summary>Gets or sets the food to eat, by item, the first carried eaten first; none chosen, what the inventory holds that only heals, the smallest heal first; with none left the life regenerates by itself up to the share.</summary>
    public IReadOnlyList<int> Food { get; set; } = [];

    /// <summary>Gets or sets whether the hunt uses a Minor Uchronic Elixir when the server reports that its alteration expired.</summary>
    public bool FarmAnomalyFragments { get; set; }

    /// <summary>Gets or sets whether the profile banks by its own rules, <see cref="FullAt"/> and its zaaps to the bank, rather than by the general settings' (D405); <see cref="UseZaaps"/> is the profile's whatever this says (D436).</summary>
    public bool OwnBanking { get; set; }

    /// <summary>Gets or sets how full the character is, in percent of its pods, once it banks, when <see cref="OwnBanking"/> is on.</summary>
    public int FullAt { get; set; } = 90;

    /// <summary>Gets or sets whether the character may take a zaap on the profile's journeys, to its place, back and to the bank; else it walks on every one (D436).</summary>
    /// <remarks>On, the journey to the bank also follows the general settings unless <see cref="OwnBanking"/> is on (D405).</remarks>
    public bool UseZaaps { get; set; } = true;

    /// <summary>Gets or sets what goes to the bank.</summary>
    public FightDeposit Deposit { get; set; } = FightDeposit.Loot;

    /// <summary>Gets or sets what the challenges of each fight give, set during the placement; the drops unless the profile says otherwise (D438).</summary>
    public ChallengeReward ChallengeReward { get; set; } = ChallengeReward.Drop;

    /// <summary>Gets or sets which challenges the AI plays for; the easy ones unless the profile says otherwise (D466).</summary>
    public ChallengePlay Challenges { get; set; } = ChallengePlay.Easy;

    /// <summary>Gets or sets what the activity does once a fight is lost.</summary>
    public FightAfterLoss AfterLostFight { get; set; } = FightAfterLoss.Resume;

    /// <summary>Gets or sets whether the run restarts the activity, once the character is put back in order, after a failure that may pass or ten minutes with nothing done; on unless the profile says otherwise (D519).</summary>
    public bool RestartAfterFailure { get; set; } = true;
}
