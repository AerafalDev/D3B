//! The behaviour of each kind of the catalogue that can run, by kind id (ADR 0017): a kind without one refuses its
//! blueprint until its PR brings it, so v2's kinds plug in here without touching the engine.

mod actions;
mod events;
mod flow;
mod functions;
mod loops;
mod memory;
mod queries;
mod values;
mod waits;

#[cfg(test)]
mod tests;

use super::behaviour::Behaviour;

/// The behaviour of `kind`, if this version runs it.
pub(super) fn behaviour(kind: &str) -> Option<&'static dyn Behaviour> {
    Some(match kind {
        "on_start" => &events::OnStart,
        "life_below" | "pods_above" | "fight_starts" | "died" | "action_failed" | "my_event" => &events::Event,
        "go_to_zone" => &actions::GoToZone,
        "harvest_until" | "hunt_until" => &actions::UntilPods,
        "go_to_bank" => &actions::GoToBank,
        "deposit_all_but" => &actions::DepositAllBut,
        "notify" => &actions::Notify,
        "if" => &flow::If,
        "wait" => &waits::Wait,
        "wait_for_life" => &waits::WaitForLife,
        "finish" => &flow::Finish,
        "sequence" => &flow::Sequence,
        "switch" => &flow::Switch,
        "repeat" => &loops::Repeat,
        "while" => &loops::While,
        "for_each" => &loops::ForEach,
        "leave_loop" => &loops::Leave,
        "do_n_times" => &memory::DoNTimes,
        "alternate" => &memory::Alternate,
        "pick" => &memory::Pick,
        "wait_until" => &waits::WaitUntil,
        "block" => &flow::Block,
        "pause_notify" => &flow::PauseNotify,
        "life_percent" => &queries::LifePercent,
        "pods_percent" => &queries::PodsPercent,
        "job_resources" => &queries::JobResources,
        "level" => &queries::Level,
        "entry" => &functions::Entry,
        "return_done" => &functions::ReturnDone,
        "return_failed" => &functions::ReturnFailed,
        "return_exit" => &functions::ReturnExit,
        "result" => &functions::PureResult,
        "greater_or_equal" => &values::GreaterOrEqual,
        "minutes" => &values::Minutes,
        "add" => &values::Add,
        _ => return None,
    })
}
