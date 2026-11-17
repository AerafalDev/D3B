use serde::Deserialize;

/// A bit of a monster's `m_flags`, as the client's `MonsterFlags` names it: its 19 bits on 3.7.13.17, read by the
/// extractor from the client's code (ADR 0010), each set on some monster but `allIdolsDisabled`. A name the app does not
/// know refuses the data folder: a new bit needs a new release.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum MonsterFlag {
    UseSummonSlot,
    UseBombSlot,
    /// A boss, 204 monsters on 3.7.13.17.
    IsBoss,
    /// A mini-boss, 306 monsters on 3.7.13.17.
    IsMiniBoss,
    /// A monster of a quest, 2,839 on 3.7.13.17.
    IsQuestMonster,
    FastAnimsFun,
    CanPlay,
    CanTackle,
    CanBePushed,
    CanSwitchPos,
    CanSwitchPosOnTarget,
    CanBeCarried,
    CanUsePortal,
    AllIdolsDisabled,
    UseRaceValues,
    SoulCaptureForbidden,
    /// Hidden in the bestiary, 48 monsters on 3.7.13.17.
    HideInBestiary,
    CanMove,
    CanBeControlledByFightOwner,
}
