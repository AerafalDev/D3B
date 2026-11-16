//! The behaviour of each kind of the catalogue that can run, by kind id (ADR 0017): a kind without one refuses its
//! blueprint until its PR brings it, so v2's kinds plug in here without touching the engine.

mod events;
mod flow;
mod functions;
mod values;

use super::behaviour::Behaviour;

/// The behaviour of `kind`, if this version runs it.
pub(super) fn behaviour(kind: &str) -> Option<&'static dyn Behaviour> {
    Some(match kind {
        "on_start" => &events::OnStart,
        "if" => &flow::If,
        "finish" => &flow::Finish,
        "entry" => &functions::Entry,
        "return_done" => &functions::ReturnDone,
        "return_failed" => &functions::ReturnFailed,
        "at_least" => &values::AtLeast,
        "add" => &values::Add,
        _ => return None,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::catalogue::KINDS;

    #[test]
    fn every_kind_that_runs_is_in_the_catalogue() {
        let runs = [
            "on_start",
            "if",
            "finish",
            "entry",
            "return_done",
            "return_failed",
            "at_least",
            "add",
        ];
        for kind in KINDS {
            assert_eq!(behaviour(kind.id).is_some(), runs.contains(&kind.id), "{}", kind.id);
        }
        assert!(behaviour("sit_until").is_none(), "a kind no catalogue holds");
    }
}
