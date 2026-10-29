namespace D3B.Samples.Hello;

/// <summary>The stroll's settings, which a profile keeps: their initial values are the defaults (D98).</summary>
internal sealed class StrollSettings
{
    /// <summary>Gets or sets how many walks a stroll takes.</summary>
    public int Walks { get; set; } = 10;

    /// <summary>Gets or sets how far, in cells, a walk goes at most.</summary>
    public int Reach { get; set; } = 4;
}
