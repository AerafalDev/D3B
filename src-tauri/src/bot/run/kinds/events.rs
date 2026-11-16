//! The events' behaviours: where a flow starts. Their priority, mode and thresholds at run time are (26)'s.

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Starts, Step};

/// On start: the main flow.
pub(super) struct OnStart;

impl Behaviour for OnStart {
    fn starts(&self) -> Option<Starts> {
        Some(Starts::Main)
    }

    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::go("then"))
    }
}
