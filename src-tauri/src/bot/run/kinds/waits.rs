//! The flow's nodes that wait: Wait, on the clock, and Wait for life, on the observed state. Each starts over when the
//! flow comes back to it after an interruption, as an action does.

use std::time::Duration;

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};
use super::queries::life_percent;

/// How often what rises with no message is read again.
const AGAIN: Duration = Duration::from_secs(1);

/// Wait: its `duration`, in seconds, from when the flow came in; 0 does not wait.
pub(super) struct Wait;

impl Behaviour for Wait {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let now = cx.now();
        if matches!(resume, Resume::Enter { .. }) {
            let seconds = u64::try_from(cx.number("duration")?).map_err(|_| Fault::NotOfType {
                pin: "duration".to_owned(),
            })?;
            let wait = Duration::from_secs(seconds);
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
/// 2026-11-14), counted by the clock with no message (`game::life`), so it is read again each second, as v2's Wait
/// until is (`docs/bot.md`).
pub(super) struct WaitForLife;

impl Behaviour for WaitForLife {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let wanted = cx.number("life")?;
        Ok(match life_percent(cx.view()) {
            Some(life) if life >= wanted => Step::go("then"),
            _ => Step::Wait {
                until: Some(cx.now() + AGAIN),
            },
        })
    }
}
