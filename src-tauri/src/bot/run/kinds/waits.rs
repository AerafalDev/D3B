//! The flow's nodes that wait: Wait, on the clock, Wait for life and Wait until, on the observed state. Each starts
//! over when the flow comes back to it after an interruption, as an action does.

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

/// Wait until: out of Then once its condition holds, read at each advance and each second, or out of Too long after its
/// `longest`, in seconds, from when the flow came in; 0 waits with no limit. It starts over after an interruption, as
/// Wait does.
pub(super) struct WaitUntil;

impl Behaviour for WaitUntil {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let now = cx.now();
        if matches!(resume, Resume::Enter { .. }) {
            let seconds = u64::try_from(cx.number("longest")?).map_err(|_| Fault::NotOfType {
                pin: "longest".to_owned(),
            })?;
            let longest = (seconds > 0)
                .then(|| now.checked_add(Duration::from_secs(seconds)).ok_or(Fault::TooLarge))
                .transpose()?;
            cx.slot().at = longest;
        }
        // A value not seen yet is neither true nor false: it waits for it (the owner, 2026-11-17), as Wait for life does.
        match cx.condition("condition") {
            Ok(true) => return Ok(Step::go("then")),
            Ok(false) | Err(Fault::NotSeen { .. }) => {}
            Err(fault) => return Err(fault),
        }
        let longest = cx.slot().at;
        Ok(match longest {
            Some(until) if now >= until => Step::go("too_long"),
            _ => Step::Wait {
                until: Some(longest.map_or(now + AGAIN, |until| until.min(now + AGAIN))),
            },
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
