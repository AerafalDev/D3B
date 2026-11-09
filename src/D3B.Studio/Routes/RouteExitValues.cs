namespace D3B.Studio.Routes;

/// <summary>A forced exit: by a side of the map, or by a cell when one is chosen (D377).</summary>
public sealed class RouteExitValues
{
    /// <summary>Gets or sets the side the character leaves by, when no cell is chosen.</summary>
    public RouteDirection Direction { get; set; }

    /// <summary>Gets or sets the cell the character leaves by, or <see langword="null"/> to leave by the side.</summary>
    public int? Cell { get; set; }
}
