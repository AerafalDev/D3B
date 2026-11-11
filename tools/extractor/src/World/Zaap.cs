namespace D3B.Extractor.World;

/// <summary>A zaap, as <c>WaypointData</c> holds it: the map it stands on.</summary>
internal sealed class Zaap
{
    public required int Id { get; init; }

    public required int MapId { get; init; }

    public required int SubAreaId { get; init; }

    public required bool Activated { get; init; }
}
