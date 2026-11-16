//! The flow's behaviours that need no clock: If and Finish. Wait and Wait for life come with the nodes that wait.

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};

/// If: out of True or False by its condition.
pub(super) struct If;

impl Behaviour for If {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::go(if cx.condition("condition")? { "true" } else { "false" }))
    }
}

/// Finish: the run is finished.
pub(super) struct Finish;

impl Behaviour for Finish {
    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::Finish)
    }
}
