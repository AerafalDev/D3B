//! v1's queries over the observed state, read when the node that uses them runs: a value not seen yet (no life read
//! since the connection opened) blocks with what is missing rather than guessing.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault};
use crate::game::View;

/// Life as a whole percentage of its maximum, rounded down.
pub(super) fn life_percent(view: &View) -> Option<i64> {
    let life = view.life?;
    percent(life.life, life.max_life)
}

/// Pods carried as a whole percentage of what the character can carry, rounded down.
pub(super) fn pods_percent(view: &View) -> Option<i64> {
    let pods = view.pods?;
    percent(i64::from(pods.weight), i64::from(pods.max_weight))
}

/// `part` of `whole` from 0 to 100, none of nothing.
fn percent(part: i64, whole: i64) -> Option<i64> {
    (whole > 0).then(|| part.clamp(0, whole) * 100 / whole)
}

fn seen(value: Option<i64>, what: &str) -> Result<Value, Fault> {
    value.map(Value::from).ok_or_else(|| Fault::NotSeen { what: what.to_owned() })
}

/// Life %.
pub(super) struct LifePercent;

impl Behaviour for LifePercent {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        seen(life_percent(cx.view()), "life")
    }
}

/// Pods %.
pub(super) struct PodsPercent;

impl Behaviour for PodsPercent {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        seen(pods_percent(cx.view()), "pods")
    }
}

/// The character's level.
pub(super) struct Level;

impl Behaviour for Level {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        seen(cx.view().who.as_ref().map(|who| i64::from(who.level)), "level")
    }
}

/// The items a profession harvests, from the game data.
pub(super) struct JobResources;

impl Behaviour for JobResources {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let job = cx.input("job")?;
        let job = job.as_u64().ok_or_else(|| Fault::NotOfType { pin: "job".to_owned() })?;
        let items = cx.knows().resources(job).ok_or_else(|| Fault::NotSeen {
            what: "resources".to_owned(),
        })?;
        Ok(Value::from(items))
    }
}
