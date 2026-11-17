//! The flow's behaviours that need no clock: If, Sequence, Switch, Block, Pause and notify, and Finish. The waits
//! come with the nodes that wait.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};

/// If: out of True or False by its condition.
pub(super) struct If;

impl Behaviour for If {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::go(if cx.condition("condition")? { "true" } else { "false" }))
    }
}

/// Sequence, as Unreal's (the owner, 2026-11-17): each output but the last runs as a chain, the next once it ends; the
/// last is the rest of the flow, so a Sequence in a loop's body ends where its last output's flow does.
pub(super) struct Sequence;

impl Behaviour for Sequence {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let next = match resume {
            // Leave the loop ends the chains up to the loop's, this one's with them: it is never told.
            Resume::Left => return Ok(Step::Leave),
            Resume::ChainEnded => cx.slot().count + 1,
            Resume::Enter { .. } | Resume::Tick => 0,
        };
        cx.slot().count = next;
        let outs = cx.outs();
        let at = usize::try_from(next).unwrap_or(usize::MAX);
        Ok(match outs.get(at) {
            Some(pin) if at + 1 < outs.len() => Step::Chain {
                pin: pin.clone(),
                breakable: false,
            },
            Some(pin) => Step::Go(pin.clone()),
            None => Step::Go(String::new()),
        })
    }
}

/// Switch: out of the first case its value is, `case_<n>`, else Otherwise.
pub(super) struct Switch;

impl Behaviour for Switch {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let value = cx.input("value")?;
        let case = cx.cases().iter().position(|case| same(case, &value));
        Ok(case.map_or_else(|| Step::go("otherwise"), |at| Step::Go(format!("case_{}", at + 1))))
    }
}

/// Whether two values are the same: numbers by their value, so `5` is `5.0`, anything else as written.
fn same(a: &Value, b: &Value) -> bool {
    match (a.as_f64(), b.as_f64()) {
        (Some(a), Some(b)) => (a - b).abs() < f64::EPSILON,
        _ => a == b,
    }
}

/// Block: the character is blocked with its reason.
pub(super) struct Block;

impl Behaviour for Block {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let reason = cx.input("reason")?;
        let reason = reason.as_str().ok_or_else(|| Fault::NotOfType {
            pin: "reason".to_owned(),
        })?;
        Ok(Step::Block(reason.to_owned()))
    }
}

/// Pause and notify: tells its text and pauses the run on itself; resumed by the player (step 4), it goes on out of
/// Then, so it pauses again only when the flow comes back to it.
pub(super) struct PauseNotify;

impl Behaviour for PauseNotify {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        if cx.slot().count == 1 {
            cx.slot().count = 0;
            return Ok(Step::go("then"));
        }
        let text = cx.input("text")?;
        let text = text
            .as_str()
            .ok_or_else(|| Fault::NotOfType { pin: "text".to_owned() })?;
        cx.notify(text);
        cx.slot().count = 1;
        Ok(Step::Pause)
    }
}

/// Finish: the run is finished.
pub(super) struct Finish;

impl Behaviour for Finish {
    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::Finish)
    }
}
