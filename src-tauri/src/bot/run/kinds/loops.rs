//! The loops (`docs/bot.md`, "The flow's kinds (v2.3)"): Repeat N times, While and For each run their body as a chain
//! Leave the loop ends, again each time its flow ends, then go out of Completed. Their counters are the node's slot, so
//! an interrupted body resumes with its index; indexes count from 1, and Repeat its count and For each its list are
//! read once, as the flow enters.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};
use super::super::frame::Body;

/// The body again, a loop's.
fn body() -> Step {
    Step::Chain {
        pin: "body".to_owned(),
        body: Body::Loop,
    }
}

/// The passes made, counted on as each ends, none as the flow enters: `None` once the loop was left.
fn passes(cx: &mut Cx<'_>, resume: &Resume) -> Option<u64> {
    let slot = cx.slot();
    match resume {
        // A failure ends the chains up to Retry's try, never a loop's alone: a loop is not told one.
        Resume::Left | Resume::Failed(_) => return None,
        Resume::ChainEnded => slot.count += 1,
        Resume::Enter { .. } | Resume::Tick => slot.count = 0,
    }
    Some(slot.count)
}

/// The index of the pass about to run, from 1.
fn index(done: u64) -> Value {
    Value::from(done.saturating_add(1))
}

/// Repeat: its body `times` times, read once as the flow enters, as For each reads its list (settled alone), its index
/// from 1; 0 or fewer goes straight on.
pub(super) struct Repeat;

impl Behaviour for Repeat {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let entering = matches!(resume, Resume::Enter { .. } | Resume::Tick);
        let Some(done) = passes(cx, &resume) else {
            return Ok(Step::go("completed"));
        };
        if entering {
            let times = u64::try_from(cx.number("times")?).unwrap_or_default();
            cx.slot().value = Some(Value::from(times));
        }
        let times = cx.slot().value.as_ref().and_then(Value::as_u64).unwrap_or_default();
        if done >= times {
            return Ok(Step::go("completed"));
        }
        cx.give("index", index(done));
        Ok(body())
    }
}

/// While: its body as long as its condition holds, read before each pass.
pub(super) struct While;

impl Behaviour for While {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        if passes(cx, &resume).is_none() || !cx.condition("condition")? {
            return Ok(Step::go("completed"));
        }
        Ok(body())
    }
}

/// For each: its body once per element of the list read as the flow entered, with the element and its index.
pub(super) struct ForEach;

impl Behaviour for ForEach {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let entering = matches!(resume, Resume::Enter { .. } | Resume::Tick);
        let Some(done) = passes(cx, &resume) else {
            return Ok(Step::go("completed"));
        };
        if entering {
            let list = cx.input("list")?;
            if !list.is_array() {
                return Err(Fault::NotOfType { pin: "list".to_owned() });
            }
            cx.slot().value = Some(list);
        }
        let at = usize::try_from(done).unwrap_or(usize::MAX);
        let element = cx.slot().value.as_ref().and_then(|list| list.get(at)).cloned();
        let Some(element) = element else {
            return Ok(Step::go("completed"));
        };
        cx.give("element", element);
        cx.give("index", index(done));
        Ok(body())
    }
}

/// Leave the loop: the nearest loop's body ends, and it goes on out of its Completed.
pub(super) struct Leave;

impl Behaviour for Leave {
    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::Leave)
    }
}
