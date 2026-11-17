//! The kinds that remember (`docs/bot.md`, "The flow's kinds (v2.3)"): Do N times, Alternate and Pick keep their
//! memory per node for the whole run, in a function called many times too (the owner, 2026-11-17), in the run's
//! `kept` rather than a call's slot.

use std::collections::HashMap;

use serde_json::{Map, Value};

use super::super::behaviour::{Behaviour, Cx, Fault, Resume, Step};

/// Nowhere: the chain ends, as at an output wired to nothing.
fn nowhere() -> Step {
    Step::Go(String::new())
}

/// Do N times: Then its first `times` runs, then nothing until its Reset input re-arms it, which goes nowhere itself.
pub(super) struct DoNTimes;

impl Behaviour for DoNTimes {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        if matches!(&resume, Resume::Enter { pin } if pin == "reset") {
            cx.kept().count = 0;
            return Ok(nowhere());
        }
        let times = u64::try_from(cx.number("times")?).unwrap_or_default();
        let kept = cx.kept();
        if kept.count >= times {
            return Ok(nowhere());
        }
        kept.count += 1;
        Ok(Step::go("then"))
    }
}

/// Alternate: First, then Second, then First, each time the flow comes in.
pub(super) struct Alternate;

impl Behaviour for Alternate {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let kept = cx.kept();
        kept.count += 1;
        Ok(Step::go(if kept.count % 2 == 1 { "first" } else { "second" }))
    }
}

/// Pick: the element of its list picked least recently, one never picked first, in the list's order; which is in turn
/// while the list does not change, an element listed twice taking two turns. Each is known by its JSON and which of
/// its copies it is; the memory forgets what left the list. An empty list cannot give one.
pub(super) struct Pick;

impl Behaviour for Pick {
    fn run(&self, cx: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        let list = cx.input("list")?;
        let Some(elements) = list.as_array().filter(|elements| !elements.is_empty()) else {
            return Err(if list.is_array() {
                Fault::Empty { pin: "list".to_owned() }
            } else {
                Fault::NotOfType { pin: "list".to_owned() }
            });
        };
        let mut copies: HashMap<String, usize> = HashMap::new();
        let keys: Vec<String> = elements
            .iter()
            .map(|element| {
                let json = element.to_string();
                let copy = copies.entry(json.clone()).or_default();
                *copy += 1;
                format!("{json}#{copy}")
            })
            .collect();
        let kept = cx.kept();
        kept.count += 1;
        let turn = kept.count;
        // When each element was last picked: none for one never picked.
        let before = kept.value.take();
        let when = |key: &String| before.as_ref().and_then(|it| it.get(key)).and_then(Value::as_u64);
        let mut picked: Map<String, Value> = keys
            .iter()
            .filter_map(|key| when(key).map(|turn| (key.clone(), Value::from(turn))))
            .collect();
        let at = (0..keys.len()).min_by_key(|&at| when(&keys[at])).unwrap_or_default();
        picked.insert(keys[at].clone(), Value::from(turn));
        cx.kept().value = Some(Value::Object(picked));
        cx.give("element", elements[at].clone());
        Ok(Step::go("then"))
    }
}
