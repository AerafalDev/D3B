namespace D3B.Extractor.Maps;

/// <summary>The booleans of a map's cell, packed one bit each in the data folder's <c>flags</c> column (docs/data.md).</summary>
/// <remarks>
/// They are fields of the cell, not an enum of the client's code: the order is ours, written down in docs/data.md and in
/// the app's reader. <c>nonWalkableDuringFight</c> and <c>nonWalkableDuringRP</c> exist only in the <c>MapMetadata</c>
/// layout (<c>main:docs/client.md</c>, "Cells").
/// </remarks>
internal static class CellFlags
{
    /// <summary>The cell's boolean fields, bit 0 first.</summary>
    public static readonly IReadOnlyList<string> Names =
    [
        "mov",
        "los",
        "nonWalkableDuringFight",
        "nonWalkableDuringRP",
        "farmCell",
        "visible",
        "havenbagCell",
        "roleplayMonstersMovementBlocked",
        "red",
        "blue",
    ];

    /// <summary>The cell's number fields the folder keeps where they are not zero, under these names.</summary>
    public static readonly IReadOnlyDictionary<string, string> Numbers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mapChangeData"] = "mapChange",
        ["altitude"] = "floor",
        ["floor"] = "floor",
        ["moveZone"] = "moveZone",
        ["arrow"] = "arrow",
        ["speed"] = "speed",
    };
}
