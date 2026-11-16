//! v1's actions, goal-shaped (ADR 0014): each waits for its check on the observed state to say it is done, so the
//! player doing it by hand completes it before the bot does (the owner, 2026-11-16); steps 4 to 6 add the doing. A
//! check on what D3B does not observe yet fails with [`unseen`]'s reason.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};
use super::queries::pods_percent;

/// Why an action fails when its check reads what D3B observes only from `step`: an id the interface words, as the
/// catalogue's (settled alone).
pub(crate) fn unseen(step: u8) -> String {
    format!("unseen_before_step_{step}")
}

/// Done once the check holds; until then the node waits on the observed state.
fn until(done: bool) -> Step {
    if done {
        Step::go("done")
    } else {
        Step::Wait { until: None }
    }
}

/// Go to a zone: done on a map of it.
pub(super) struct GoToZone;

impl Behaviour for GoToZone {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let zone = cx.input("zone")?;
        let map = cx.view().map_id;
        Ok(until(map.and_then(|map| cx.knows().in_zone(&zone, map)) == Some(true)))
    }
}

/// Harvest until, and Hunt until: done once the pods reach `pods` %. Their harvests' count is not observed before
/// step 5: it is not given, and a node reading it blocks.
pub(super) struct UntilPods;

impl Behaviour for UntilPods {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let wanted = cx.number("pods")?;
        cx.input("zone")?;
        Ok(until(pods_percent(cx.view()).is_some_and(|pods| pods >= wanted)))
    }
}

/// Go to the bank: done on its map; the nearest one is known from step 4's paths.
pub(super) struct GoToBank;

impl Behaviour for GoToBank {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let bank = cx.input("bank")?;
        let Some(map) = bank.get("map").and_then(Value::as_i64) else {
            return Ok(Step::Failed(unseen(4)));
        };
        Ok(until(cx.view().map_id == Some(map)))
    }
}

/// Deposit all but: the bank's and the inventory's content are observed from step 5.
pub(super) struct DepositAllBut;

impl Behaviour for DepositAllBut {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        cx.input("keep")?;
        Ok(Step::Failed(unseen(5)))
    }
}

/// Notify: its text is told, then done at once.
pub(super) struct Notify;

impl Behaviour for Notify {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let text = cx.input("text")?;
        cx.notify(text.as_str().unwrap_or_default());
        Ok(Step::go("done"))
    }
}
