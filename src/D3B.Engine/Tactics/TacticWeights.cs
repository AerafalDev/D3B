namespace D3B.Engine.Tactics;

/// <summary>How much the AI weighs what it fears against what it wants: the damage the enemies may deal next, the damage it may deal next turn, its own life.</summary>
/// <remarks>
/// <see cref="Default"/> fights; <see cref="Prudent"/>, for a fight judged too risky, the owner's choice (D198), fears the
/// enemies' blows more and holds its own life dearer, so that it heals, shields and keeps its distance sooner; <see cref="Mule"/>
/// places a character that never acts.
/// </remarks>
public sealed record TacticWeights
{
    private readonly double _approach = 0.9;

    /// <summary>Gets the weights of a fight the AI expects to win.</summary>
    public static TacticWeights Default { get; } = new();

    /// <summary>Gets the weights of a fight the AI expects to win beside allies of its own group, a little bolder than <see cref="Default"/>.</summary>
    /// <remarks>
    /// The owner's ask from game (D454): in a group the characters were "un poil trop sur la défensive". The enemies' blows
    /// spread over several allies, so the life they may take weighs less. D454 also weighed the next turn's damage at 1.1,
    /// which left whole the monsters the characters could kill now: measured on the owner's group run (D460), 15 turns of 87
    /// passed with a monster in reach, so the approach is the defaults'. 10d measures the group's fights and sets the
    /// threat (D434).
    /// </remarks>
    public static TacticWeights Group { get; } = new() { Threat = 0.75 };

    /// <summary>Gets the weights of a fight judged too risky.</summary>
    public static TacticWeights Prudent { get; } = new() { Threat = 1.6, Survival = 3, Approach = 0.5 };

    /// <summary>Gets the weights of a mule, which passes its turns (D579): it fears the enemies' blows as <see cref="Prudent"/> does, and weighs no reach of its own.</summary>
    public static TacticWeights Mule { get; } = Prudent with { Approach = 0 };

    /// <summary>Gets the weight of the life the enemies may take next, before the fighter playing plays again.</summary>
    public double Threat { get; init; } = 1;

    /// <summary>Gets the weight of the damage the fighter playing may deal next turn from where it ends this one.</summary>
    /// <remarks>
    /// Measured live on the owner's first fights (D203): at a quarter, the AI kept out of reach of enemies it beat, walking away
    /// and passing turns; next turn's damage is weighed nearly as an enemy's life is, so that it trades a blow for a stronger one.
    /// Raised from 0.8 to 0.9 (D390): a Crâ at level 2 still pushed its enemy away turn after turn instead of finishing it.
    /// It stays under 1 (D460): the judge bounds next turn's damage by what killing the enemy now would take off its cost
    /// (D203), so at 1 a blow next turn is worth a blow now and ending the turn keeps its tie (D407), and above 1 the AI
    /// leaves whole a monster it can kill now. Measured on the owner's group run at 1.1, 15 turns of 87
    /// passed with a monster in reach, and the plans dealt half the damage.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The weight is 1 or more, negative or not a number.</exception>
    public double Approach
    {
        get => _approach;
        init
        {
            // Written so that NaN, which every comparison fails, is refused too: it would judge every plan NaN.
            if (!(value is >= 0 and < 1))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Next turn's damage is weighed from 0 to under 1.");

            _approach = value;
        }
    }

    /// <summary>Gets the share of its most life above which a fighter's life points are worth only <see cref="ComfortWorth"/> of what the curve gives.</summary>
    /// <remarks>
    /// Reported by players (D407): a life steal of 8 damage healing 4 beat a blow of 14 with the character at 97% of its life,
    /// and one of 16 at 90%, since a point of its life was worth more than a point of damage even when nothing was lacking.
    /// </remarks>
    public double Comfort { get; init; } = 0.6;

    /// <summary>Gets the part of a life point's worth that remains above <see cref="Comfort"/>, 1 keeping the curve whole.</summary>
    public double ComfortWorth { get; init; } = 0.25;

    /// <summary>Gets the weight of the life the side playing's own casts take from its other fighters, on top of what that life is worth to them.</summary>
    /// <remarks>
    /// The owner's report from a group game (D485), a character striking its ally: above <see cref="Comfort"/> of its most life
    /// an ally's point is worth a quarter of the curve's, 0.16 at its most, where a point dealt an enemy is worth 0.6 to 1, so a
    /// zone striking an enemy and its ally beat a blow on the enemy alone. At 1, a point taken from an ally costs at least what
    /// a point dealt the hardest-hitting enemy is worth; a summon's life, worth nothing of itself (D270), costs nothing more.
    /// </remarks>
    public double AlliesStruck { get; init; } = 1;

    /// <summary>Gets how much dearer the fighter playing holds its own life than an ally's: its death ends its fight.</summary>
    public double Survival { get; init; } = 2;
}
