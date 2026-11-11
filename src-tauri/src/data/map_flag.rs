use serde::Deserialize;

/// A bit of a map's `m_flags`, as the client's `MapInformationFlags` names it (3.7.13.17, read by the extractor from
/// the client's code, ADR 0010). A name the app does not know refuses the data folder: a new bit needs a new release.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum MapFlag {
    CapabilityAllowChallenge,
    CapabilityAllowAggression,
    /// A character may teleport to the map (bit 2).
    CapabilityAllowTeleportTo,
    /// A character may teleport from the map (bit 3), measured on the C# version.
    CapabilityAllowTeleportFrom,
    CapabilityAllowExchangesBetweenPlayers,
    CapabilityAllowHumanVendor,
    CapabilityAllowCollector,
    CapabilityAllowSoulCapture,
    CapabilityAllowSoulSummon,
    CapabilityAllowTavernRegen,
    CapabilityAllowTombMode,
    CapabilityAllowTeleportEverywhere,
    CapabilityAllowFightChallenges,
    CapabilityAllowMonsterRespawn,
    CapabilityAllowMonsterFight,
    CapabilityAllowMount,
    CapabilityAllowObjectDisposal,
    CapabilityAllowUnderwater,
    CapabilityAllowPvp1V1,
    CapabilityAllowPvp3V3,
    CapabilityAllowMonsterAggression,
    Outdoor,
    ShowNameOnFingerpost,
    HasPriorityOnWorldmap,
    AllowPrism,
    IsTransition,
    MapHasTemplate,
    HasPublicPaddock,
    ForbidOverlayCharactersMode,
}
