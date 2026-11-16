//! The flow's nodes that wait: Wait, on the clock, and Wait for life, on the observed state. Each starts over when the
//! flow comes back to it after an interruption, as an action does.

use std::time::Duration;

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};
use super::queries::life_percent;

/// Wait: `minutes` from when the flow came in; 0 does not wait.
pub(super) struct Wait;

impl Behaviour for Wait {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let now = cx.now();
        if matches!(resume, Resume::Enter { .. }) {
            let minutes = u64::try_from(cx.number("minutes")?).map_err(|_| Fault::NotOfType {
                pin: "minutes".to_owned(),
            })?;
            let wait = Duration::from_secs(minutes.checked_mul(60).ok_or(Fault::TooLarge)?);
            cx.slot().at = Some(now.checked_add(wait).ok_or(Fault::TooLarge)?);
        }
        let until = cx.slot().at.unwrap_or(now);
        Ok(if now >= until {
            Step::go("then")
        } else {
            Step::Wait { until: Some(until) }
        })
    }
}

/// Wait for life: until the life is at `life` % of its maximum; it comes back alone, out of a fight (the owner,
/// 2026-11-14).
pub(super) struct WaitForLife;

impl Behaviour for WaitForLife {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let wanted = cx.number("life")?;
        Ok(match life_percent(cx.view()) {
            Some(life) if life >= wanted => Step::go("then"),
            _ => Step::Wait { until: None },
        })
    }
}
