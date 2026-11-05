namespace D3B.Data.WorldMaps;

/// <summary>The coordinates of a world's layer that hold maps, and how many (D299).</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
/// <param name="Count">How many maps stand on the coordinates in the layer, the insides it holds included.</param>
public readonly record struct WorldMapSquare(int X, int Y, int Count)
{
    /// <summary>Gets whether several maps stand there, which the editor marks and asks to choose among (D299).</summary>
    public bool IsShared => Count > 1;
}
