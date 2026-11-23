//! What the runtime asks of the game data and the player's files, behind a trait so that the domains stay apart and the
//! tests answer it by hand: step 4's actor answers it from `data::World` and `storage`'s zones.

use serde_json::Value;

use crate::bot::ValueType;

/// What a run knows beyond the observed state.
pub(crate) trait Knows {
    /// Whether `map` is in `zone` (a game's sub-area or a zone of mine), `None` when unknown: no data of the
    /// connection's build, or a zone of mine gone.
    fn in_zone(&self, zone: &Value, map: i64) -> Option<bool>;

    /// The items profession `job` harvests, from the data's skills; `None` when unknown.
    fn resources(&self, job: u64) -> Option<Vec<u64>>;

    /// A value of `of`'s words in the player's language, as a Text says it: a zone's, an item's, a profession's or a map's
    /// name, yes or no; `None` when unknown, and the Text says its id (the owner, 2026-11-17).
    fn words(&self, of: ValueType, value: &Value) -> Option<String> {
        let _ = (of, value);
        None
    }
}

/// Knows nothing: what a blueprint reading no zone and no profession needs, in tests.
#[cfg(test)]
pub(super) struct Nothing;

#[cfg(test)]
impl Knows for Nothing {
    fn in_zone(&self, _: &Value, _: i64) -> Option<bool> {
        None
    }

    fn resources(&self, _: u64) -> Option<Vec<u64>> {
        None
    }
}
