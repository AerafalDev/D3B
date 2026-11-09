namespace D3B.App.Controls.WorldMaps;

/// <summary>What a step of a route does, as the small dot under its node tells at a glance (D397).</summary>
internal enum WorldMapActionKind
{
    /// <summary>It harvests.</summary>
    Harvest,

    /// <summary>It fights.</summary>
    Fight,

    /// <summary>It goes through a door, stairs or an exit.</summary>
    Door,

    /// <summary>It speaks to an NPC.</summary>
    Npc,

    /// <summary>Anything else: a zaap, the haven bag, following another route.</summary>
    Other,
}
