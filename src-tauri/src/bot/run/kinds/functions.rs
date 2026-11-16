//! A function's entry and returns: its inputs are its entry's outputs, given at the call; a return gives back its
//! outputs and ends the call Done or Failed.

use std::collections::BTreeMap;

use super::super::behaviour::{Behaviour, Cx, Exit, Fault, Resume, Starts, Step};

/// The entry: where a call's flow starts.
pub(super) struct Entry;

impl Behaviour for Entry {
    fn starts(&self) -> Option<Starts> {
        Some(Starts::Call)
    }

    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::go("then"))
    }
}

/// Done: the call goes on out of its Done, with the function's outputs, its inputs here.
pub(super) struct ReturnDone;

impl Behaviour for ReturnDone {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let mut outputs = BTreeMap::new();
        for pin in cx.inputs() {
            let value = cx.input(&pin)?;
            outputs.insert(pin, value);
        }
        Ok(Step::Return {
            exit: Exit::Done,
            outputs,
        })
    }
}

/// Failed: the call goes on out of its Failed with this reason, or blocks the character.
pub(super) struct ReturnFailed;

impl Behaviour for ReturnFailed {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let reason = cx.input("reason")?;
        let reason = reason.as_str().unwrap_or_default().to_owned();
        Ok(Step::Return {
            exit: Exit::Failed(reason),
            outputs: BTreeMap::new(),
        })
    }
}
