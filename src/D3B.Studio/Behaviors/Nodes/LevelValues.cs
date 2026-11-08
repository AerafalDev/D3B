namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A level, and the profession it is of, if any.</summary>
public sealed class LevelValues
{
    /// <summary>Gets or sets the profession, for a profession's level; none for the character's.</summary>
    public int? Job { get; set; }

    /// <summary>Gets or sets the level, 1 by default.</summary>
    public int Level { get; set; } = 1;
}
