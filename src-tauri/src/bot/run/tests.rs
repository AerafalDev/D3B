//! The engine on a fixed clock: blueprints written as their files, and programs built by hand with kinds of the tests'
//! own (Sequence, Repeat, Leave the loop, a wait) that prove the stack holds v2's flow before v2's kinds exist.

use std::collections::BTreeMap;
use std::time::{Duration, Instant};

use serde_json::{Value, json};

use super::frame::Slot;
use super::knows::Nothing;
use super::program::{Code, NodeCode, Source};
use super::*;
use crate::bot::{Id, read};

fn blueprint(variables: &str, graphs: &str) -> Blueprint {
    let text = format!(
        r#"{{"schema": 1, "id": "test", "name": "Test", "version": 1, "variables": {variables}, "graphs": {graphs}}}"#
    );
    read(&text).unwrap_or_else(|error| panic!("{error}"))
}

/// A memory variable `n`, a number from 0, and a setting `flag`, yes or no, true by default.
const VARIABLES: &str = r#"[
  {"id": "n", "name": "N", "type": "number", "default": 0, "memory": true},
  {"id": "flag", "name": "Flag", "type": "bool", "default": true, "per_character": true}
]"#;

fn run_of(blueprint: &Blueprint, settings: &BTreeMap<String, Value>) -> Run {
    Run::new(blueprint, settings, &BTreeMap::new()).unwrap_or_else(|error| panic!("{error:?}"))
}

fn n(run: &mut Run) -> Option<Value> {
    run.memory().0.get("n").cloned()
}

fn blocked(run: &Run) -> Option<(Reason, &str)> {
    match run.state() {
        State::Blocked(blocked) => Some((blocked.reason.clone(), blocked.node.as_str())),
        _ => None,
    }
}

/// On start → If [flag] → True: n ← 1 → Finish; False: n ← 2, its flow ending there.
const IF: &str = r#"[{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "flag", "get": "flag"}, {"id": "if", "kind": "if"},
            {"id": "one", "set": "n", "inputs": {"value": 1}}, {"id": "two", "set": "n", "inputs": {"value": 2}},
            {"id": "end", "kind": "finish"}],
  "wires": [{"from": ["start", "then"], "to": ["if", "run"]}, {"from": ["flag", "value"], "to": ["if", "condition"]},
            {"from": ["if", "true"], "to": ["one", "run"]}, {"from": ["if", "false"], "to": ["two", "run"]},
            {"from": ["one", "then"], "to": ["end", "run"]}]}]"#;

#[test]
fn if_follows_its_condition_read_from_the_characters_setting() {
    let now = Instant::now();
    let view = View::default();
    let mut run = run_of(&blueprint(VARIABLES, IF), &BTreeMap::new());
    assert_eq!(run.state(), &State::Ready);
    assert_eq!(run.advance(Some(&view), &Nothing, now), &State::Finished);
    assert_eq!(n(&mut run), Some(json!(1)));
    let read: Vec<_> = run
        .trace()
        .iter()
        .map(|it| (it.node.0.as_str(), it.pin.as_str(), &it.value))
        .collect();
    assert_eq!(read, [("if", "condition", &json!(true)), ("one", "value", &json!(1))]);

    let settings = BTreeMap::from([("flag".to_owned(), json!(false))]);
    let mut run = run_of(&blueprint(VARIABLES, IF), &settings);
    // The main flow ending on an output wired to nothing finishes the run too.
    assert_eq!(run.advance(Some(&view), &Nothing, now), &State::Finished);
    let (memory, changed) = run.memory();
    assert_eq!((memory.get("n"), changed), (Some(&json!(2)), true), "memory changed");
    assert!(!run.memory().1, "and told once");
    // A finished run stays so.
    assert_eq!(run.advance(Some(&view), &Nothing, now), &State::Finished);
}

#[test]
fn memory_and_settings_start_the_run_and_a_value_refused_is_left_out() {
    let blueprint = blueprint(VARIABLES, IF);
    let memory = BTreeMap::from([("n".to_owned(), json!(7))]);
    let run = Run::new(&blueprint, &BTreeMap::new(), &memory);
    let mut run = run.unwrap_or_else(|error| panic!("{error}"));
    assert_eq!(n(&mut run), Some(json!(7)));
    // Of another type, a setting is left out and the default holds.
    let settings = BTreeMap::from([("flag".to_owned(), json!(3))]);
    let mut run = run_of(&blueprint, &settings);
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    assert_eq!(n(&mut run), Some(json!(1)));
}

#[test]
fn a_blueprint_that_cannot_run_is_refused_with_why() {
    let refused = |variables: &str, graphs: &str| {
        Run::new(&blueprint(variables, graphs), &BTreeMap::new(), &BTreeMap::new()).err()
    };
    let problem = r#"[{"id": "events", "name": "Events", "nodes": [{"id": "start", "kind": "on_start"},
      {"id": "if", "kind": "if"}], "wires": [{"from": ["start", "then"], "to": ["if", "run"]}]}]"#;
    assert!(matches!(refused("[]", problem), Some(Refused::Problems(found)) if !found.is_empty()));
    // Every kind of the catalogue runs (`kinds`): a kind without behaviour, as a v2 kind before its PR, is refused.
    let empty = r#"[{"id": "events", "name": "Events"}]"#;
    assert_eq!(refused("[]", empty), Some(Refused::NoStart));
    let unset = r#"[{"id": "zone", "name": "Zone", "type": "zone", "per_character": true}]"#;
    let start = r#"[{"id": "events", "name": "Events", "nodes": [{"id": "start", "kind": "on_start"}]}]"#;
    assert_eq!(
        refused(unset, start),
        Some(Refused::Unset {
            variable: "zone".to_owned()
        })
    );
    let zone = BTreeMap::from([("zone".to_owned(), json!({"sub_area": 445}))]);
    assert!(Run::new(&blueprint(unset, start), &zone, &BTreeMap::new()).is_ok());
}

/// A function `plus` of input `x` and output `y` = x + 1, called on n, then n ← its y; `fail` instead fails with a
/// reason, its call's Failed wired or not.
fn calls(call: &str, failed_wired: bool) -> String {
    let (why, wire) = if failed_wired {
        (
            r#", {"id": "why", "set": "text"}"#,
            r#", {"from": ["call", "failed"], "to": ["why", "run"]}, {"from": ["call", "reason"], "to": ["why", "value"]}"#,
        )
    } else {
        ("", "")
    };
    format!(
        r#"[{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "get", "get": "n"}}, {{"id": "call", "call": "{call}"}},
            {{"id": "set", "set": "n"}}{why}],
  "wires": [{{"from": ["start", "then"], "to": ["call", "run"]}}, {{"from": ["get", "value"], "to": ["call", "x"]}},
            {{"from": ["call", "done"], "to": ["set", "run"]}}, {{"from": ["call", "y"], "to": ["set", "value"]}}{wire}]}},
 {{"id": "plus", "name": "Plus", "function": {{"inputs": [{{"id": "x", "name": "X", "type": "number"}}],
   "outputs": [{{"id": "y", "name": "Y", "type": "number"}}]}},
  "nodes": [{{"id": "in", "kind": "entry"}}, {{"id": "add", "kind": "add", "inputs": {{"b": 1}}}},
            {{"id": "out", "kind": "return_done"}}],
  "wires": [{{"from": ["in", "then"], "to": ["out", "run"]}}, {{"from": ["in", "x"], "to": ["add", "a"]}},
            {{"from": ["add", "value"], "to": ["out", "y"]}}]}},
 {{"id": "fail", "name": "Fail", "function": {{"inputs": [{{"id": "x", "name": "X", "type": "number"}}],
   "outputs": [{{"id": "y", "name": "Y", "type": "number"}}]}},
  "nodes": [{{"id": "in", "kind": "entry"}}, {{"id": "out", "kind": "return_failed", "inputs": {{"reason": "no bank"}}}}],
  "wires": [{{"from": ["in", "then"], "to": ["out", "run"]}}]}}]"#
    )
}

const WITH_TEXT: &str = r#"[
  {"id": "n", "name": "N", "type": "number", "default": 0, "memory": true},
  {"id": "text", "name": "Text", "type": "text", "default": "", "memory": true}
]"#;

#[test]
fn a_call_gives_its_inputs_to_its_entry_and_takes_back_its_outputs() {
    let memory = BTreeMap::from([("n".to_owned(), json!(41))]);
    let run = Run::new(&blueprint(WITH_TEXT, &calls("plus", false)), &BTreeMap::new(), &memory);
    let mut run = run.unwrap_or_else(|error| panic!("{error:?}"));
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    assert_eq!(n(&mut run), Some(json!(42)));
}

#[test]
fn a_function_failed_follows_its_calls_failed_or_blocks_there() {
    let now = Instant::now();
    let mut run = run_of(&blueprint(WITH_TEXT, &calls("fail", true)), &BTreeMap::new());
    assert_eq!(run.advance(Some(&View::default()), &Nothing, now), &State::Finished);
    assert_eq!(run.memory().0.get("text"), Some(&json!("no bank")));

    let mut run = run_of(&blueprint(WITH_TEXT, &calls("fail", false)), &BTreeMap::new());
    run.advance(Some(&View::default()), &Nothing, now);
    assert_eq!(blocked(&run), Some((Reason::Failed("no bank".to_owned()), "call")));
}

/// On start, two calls of `full` each going on out of its exit `full`, n ← their y in turn; `full` adds its x to its
/// local `tries`, 10 at each call, and returns by `full` with it.
const EXITS: &str = r#"[{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "g1", "get": "n"}, {"id": "c1", "call": "full"},
            {"id": "s1", "set": "n"}, {"id": "c2", "call": "full"}, {"id": "g2", "get": "n"}, {"id": "s2", "set": "n"},
            {"id": "end", "kind": "finish"}],
  "wires": [{"from": ["start", "then"], "to": ["c1", "run"]}, {"from": ["g1", "value"], "to": ["c1", "x"]},
            {"from": ["c1", "full"], "to": ["s1", "run"]}, {"from": ["c1", "y"], "to": ["s1", "value"]},
            {"from": ["s1", "then"], "to": ["c2", "run"]}, {"from": ["g2", "value"], "to": ["c2", "x"]},
            {"from": ["c2", "full"], "to": ["s2", "run"]}, {"from": ["c2", "y"], "to": ["s2", "value"]},
            {"from": ["s2", "then"], "to": ["end", "run"]}]},
 {"id": "full", "name": "Full", "function": {"inputs": [{"id": "x", "name": "X", "type": "number"}],
   "outputs": [{"id": "y", "name": "Y", "type": "number"}], "exits": [{"id": "full", "name": "Full"}],
   "locals": [{"id": "tries", "name": "Tries", "type": "number", "default": 10}]},
  "nodes": [{"id": "in", "kind": "entry"}, {"id": "gt", "get": "tries"}, {"id": "add", "kind": "add"},
            {"id": "st", "set": "tries"}, {"id": "gt2", "get": "tries"},
            {"id": "out", "kind": "return_exit", "options": {"exit": "full"}}],
  "wires": [{"from": ["in", "then"], "to": ["st", "run"]}, {"from": ["gt", "value"], "to": ["add", "a"]},
            {"from": ["in", "x"], "to": ["add", "b"]}, {"from": ["add", "value"], "to": ["st", "value"]},
            {"from": ["st", "then"], "to": ["out", "run"]}, {"from": ["gt2", "value"], "to": ["out", "y"]}]}]"#;

#[test]
fn a_return_by_an_exit_goes_on_out_of_it_and_locals_start_from_their_defaults_at_each_call() {
    let memory = BTreeMap::from([("n".to_owned(), json!(3))]);
    let run = Run::new(&blueprint(VARIABLES, EXITS), &BTreeMap::new(), &memory);
    let mut run = run.unwrap_or_else(|error| panic!("{error:?}"));
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    // 10 + 3, then 10 + 13: the second call's local starts again from 10, else it would be 13 + 13.
    assert_eq!(n(&mut run), Some(json!(23)));
}

#[test]
fn a_blueprint_calling_a_pure_function_is_refused_until_the_engine_reads_one() {
    let graphs = r#"[{"id": "events", "name": "Events",
      "nodes": [{"id": "start", "kind": "on_start"}, {"id": "h", "call": "half", "inputs": {"a": 2}}]},
     {"id": "half", "name": "Half", "function": {"inputs": [{"id": "a", "name": "A", "type": "number"}], "pure": true},
      "nodes": [{"id": "in", "kind": "entry"}]}]"#;
    let refused = Run::new(&blueprint(VARIABLES, graphs), &BTreeMap::new(), &BTreeMap::new()).err();
    let kind = match refused {
        Some(super::program::Refused::NotRunnable { kind }) => Some(kind),
        _ => None,
    };
    assert_eq!(kind.as_deref(), Some("result"));
}

#[test]
fn a_function_calling_itself_without_end_blocks_past_the_deepest_call() {
    // Each call counts itself, then calls again.
    let graphs = r#"[{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "call", "call": "again"}],
  "wires": [{"from": ["start", "then"], "to": ["call", "run"]}]},
 {"id": "again", "name": "Again", "function": {},
  "nodes": [{"id": "in", "kind": "entry"}, {"id": "get", "get": "n"}, {"id": "add", "kind": "add", "inputs": {"b": 1}},
            {"id": "count", "set": "n"}, {"id": "call", "call": "again"}],
  "wires": [{"from": ["in", "then"], "to": ["count", "run"]}, {"from": ["get", "value"], "to": ["add", "a"]},
            {"from": ["add", "value"], "to": ["count", "value"]}, {"from": ["count", "then"], "to": ["call", "run"]}]}]"#;
    let mut run = run_of(&blueprint(VARIABLES, graphs), &BTreeMap::new());
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    assert_eq!(blocked(&run), Some((Reason::TooDeep, "call")));
    assert_eq!(
        n(&mut run),
        Some(json!(DEEPEST)),
        "as many calls at work as allowed, not one more"
    );
}

#[test]
fn a_set_out_of_its_variables_bounds_blocks_rather_than_lose_it_from_memory() {
    let variables = r#"[{"id": "n", "name": "N", "type": "number", "default": 0, "bounds": [0, 10], "memory": true}]"#;
    let graphs = |value: i64| {
        format!(
            r#"[{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "set", "set": "n", "inputs": {{"value": {value}}}}}],
  "wires": [{{"from": ["start", "then"], "to": ["set", "run"]}}]}}]"#
        )
    };
    let mut run = run_of(&blueprint(variables, &graphs(10)), &BTreeMap::new());
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    assert_eq!(n(&mut run), Some(json!(10)));
    let mut run = run_of(&blueprint(variables, &graphs(11)), &BTreeMap::new());
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    let refused = Fault::Refused {
        variable: "n".to_owned(),
    };
    assert_eq!(blocked(&run), Some((Reason::Fault(refused), "set")));
    assert_eq!(n(&mut run), Some(json!(0)), "nothing set");
}

#[test]
fn a_flow_that_never_waits_blocks_runs_without_pause() {
    // n ← n + 1, wired back to itself.
    let graphs = r#"[{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "get", "get": "n"},
            {"id": "add", "kind": "add", "inputs": {"b": 1}}, {"id": "set", "set": "n"}],
  "wires": [{"from": ["start", "then"], "to": ["set", "run"]}, {"from": ["get", "value"], "to": ["add", "a"]},
            {"from": ["add", "value"], "to": ["set", "value"]}, {"from": ["set", "then"], "to": ["set", "run"]}]}]"#;
    let mut run = run_of(&blueprint(VARIABLES, graphs), &BTreeMap::new());
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    assert_eq!(blocked(&run), Some((Reason::WithoutPause, "set")));
    // On start ran once, then the set: fewer would block too early.
    assert_eq!(n(&mut run), Some(json!(WITHOUT_PAUSE - 1)));
    assert_eq!(run.trace().iter().count(), Trace::KEPT, "the trace is bounded");
}

#[test]
fn a_value_that_cannot_be_computed_blocks_on_its_node() {
    let graphs = r#"[{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "add", "kind": "add", "inputs": {"a": 9223372036854775807, "b": 1}},
            {"id": "set", "set": "n"}],
  "wires": [{"from": ["start", "then"], "to": ["set", "run"]}, {"from": ["add", "value"], "to": ["set", "value"]}]}]"#;
    let mut run = run_of(&blueprint(VARIABLES, graphs), &BTreeMap::new());
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    assert_eq!(
        blocked(&run),
        Some((Reason::Fault(Fault::TooLarge), "add")),
        "on the node that failed"
    );
    assert_eq!(n(&mut run), Some(json!(0)), "nothing set");
}

// The tests' own kinds, as v2 will bring them.

/// Sequence of two, numbered from 1 as v2's: `then_1`, then `then_2`, then `completed`.
struct Sequence;

impl Behaviour for Sequence {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let slot = cx.slot();
        match resume {
            Resume::ChainEnded if slot.count == 0 => {
                slot.count = 1;
                Ok(Step::Chain {
                    pin: "then_2".to_owned(),
                    breakable: false,
                })
            }
            Resume::ChainEnded | Resume::Left => Ok(Step::go("completed")),
            Resume::Enter { .. } | Resume::Tick => {
                *slot = Slot::default();
                Ok(Step::Chain {
                    pin: "then_1".to_owned(),
                    breakable: false,
                })
            }
        }
    }
}

/// Repeat `times`: its body, breakable, then `completed`.
struct Repeat;

impl Behaviour for Repeat {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let times = u64::try_from(cx.number("times")?).unwrap_or_default();
        let slot = cx.slot();
        match resume {
            Resume::Left => return Ok(Step::go("completed")),
            Resume::ChainEnded => slot.count += 1,
            Resume::Enter { .. } | Resume::Tick => *slot = Slot::default(),
        }
        Ok(if slot.count < times {
            Step::Chain {
                pin: "body".to_owned(),
                breakable: true,
            }
        } else {
            Step::go("completed")
        })
    }
}

struct Leave;

impl Behaviour for Leave {
    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(Step::Leave)
    }
}

/// Waits a second from when the flow came in.
struct Second;

impl Behaviour for Second {
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let now = cx.now();
        let slot = cx.slot();
        if matches!(resume, Resume::Enter { .. }) {
            slot.at = Some(now + Duration::from_secs(1));
        }
        let until = slot.at.unwrap_or(now);
        Ok(if now >= until {
            Step::go("then")
        } else {
            Step::Wait { until: Some(until) }
        })
    }
}

/// An action that fails with its reason, or a Block node, by its `then` wired or not.
struct Fails(bool);

impl Behaviour for Fails {
    fn run(&self, _: &mut Cx<'_>, _: Resume) -> Result<Step, Fault> {
        Ok(if self.0 {
            Step::Failed("lost".to_owned())
        } else {
            Step::Block("stop here".to_owned())
        })
    }
}

/// A program built by hand in one event graph, its first node where the main flow starts.
#[derive(Default)]
struct Built(Vec<NodeCode>);

impl Built {
    fn kind(self, id: &str, behaviour: &'static dyn Behaviour) -> Self {
        self.node(id, Doing::Kind(behaviour), false)
    }

    fn node(mut self, id: &str, doing: Doing, pure: bool) -> Self {
        self.0.push(NodeCode {
            id: Id(id.to_owned()),
            doing,
            flows: Vec::new(),
            inputs: Vec::new(),
            pure,
            named: None,
        });
        self
    }

    fn flow(mut self, from: usize, pin: &str, to: usize) -> Self {
        self.0[from].flows.push((pin.to_owned(), (to, "run".to_owned())));
        self
    }

    fn input(mut self, node: usize, pin: &str, source: Source) -> Self {
        self.0[node].inputs.push((pin.to_owned(), source));
        self
    }

    /// n ← n + `by`, its node `at`, reading n through node `at + 1` and adding at `at + 2`.
    fn bump(self, at: usize, by: i64) -> Self {
        let add = kinds::behaviour("add").unwrap_or_else(|| unreachable!());
        self.node("bump", Doing::Set("n".to_owned()), false)
            .node("n", Doing::Get("n".to_owned()), true)
            .node("add", Doing::Kind(add), true)
            .input(at, "value", Source::Wire(at + 2, "value".to_owned()))
            .input(at + 2, "a", Source::Wire(at + 1, "value".to_owned()))
            .input(at + 2, "b", Source::Value(json!(by)))
    }

    fn run(self) -> Run {
        let program = Program {
            graphs: vec![Code {
                id: Id("events".to_owned()),
                nodes: self.0,
                entry: None,
                locals: Vec::new(),
            }],
            main: (0, 0),
        };
        let variables: Vec<crate::bot::variable::Variable> =
            serde_json::from_str(VARIABLES).unwrap_or_else(|error| panic!("{error}"));
        let variables = Variables::new(&variables, &BTreeMap::new(), &BTreeMap::new());
        Run::of(program, variables.unwrap_or_else(|error| panic!("{error}")))
    }
}

fn start() -> &'static dyn Behaviour {
    kinds::behaviour("on_start").unwrap_or_else(|| unreachable!())
}

#[test]
fn a_sequence_runs_its_outputs_in_turn_and_a_loop_its_body_each_time() {
    // On start → Sequence: 1 → n + 1; 2 → Repeat 3 → n + 10; completed → n + 100.
    let mut run = Built::default()
        .kind("start", start())
        .kind("sequence", &Sequence)
        .bump(2, 1)
        .kind("repeat", &Repeat)
        .bump(6, 10)
        .bump(9, 100)
        .flow(0, "then", 1)
        .flow(1, "then_1", 2)
        .flow(1, "then_2", 5)
        .input(5, "times", Source::Value(json!(3)))
        .flow(5, "body", 6)
        .flow(5, "completed", 9)
        .run();
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    assert_eq!(n(&mut run), Some(json!(131)));

    // A body wired to nothing ends at once, each turn: the loop still reaches its completed.
    let mut run = Built::default()
        .kind("start", start())
        .kind("repeat", &Repeat)
        .bump(2, 100)
        .flow(0, "then", 1)
        .input(1, "times", Source::Value(json!(3)))
        .flow(1, "completed", 2)
        .run();
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    assert_eq!(n(&mut run), Some(json!(100)));
}

#[test]
fn leave_the_loop_goes_to_its_completed_and_outside_one_blocks() {
    // Repeat 5 → Sequence: 1 → n + 1; 2 → Leave; completed → n + 100: the Sequence inside is left too.
    let mut run = Built::default()
        .kind("start", start())
        .kind("repeat", &Repeat)
        .kind("sequence", &Sequence)
        .bump(3, 1)
        .kind("leave", &Leave)
        .bump(7, 100)
        .flow(0, "then", 1)
        .input(1, "times", Source::Value(json!(5)))
        .flow(1, "body", 2)
        .flow(2, "then_1", 3)
        .flow(2, "then_2", 6)
        .flow(1, "completed", 7)
        .run();
    assert_eq!(
        run.advance(Some(&View::default()), &Nothing, Instant::now()),
        &State::Finished
    );
    assert_eq!(n(&mut run), Some(json!(101)));

    let mut run = Built::default()
        .kind("start", start())
        .kind("leave", &Leave)
        .flow(0, "then", 1)
        .run();
    run.advance(Some(&View::default()), &Nothing, Instant::now());
    assert_eq!(blocked(&run), Some((Reason::Fault(Fault::NotInALoop), "leave")));
}

#[test]
fn an_actions_failed_goes_on_with_its_reason_or_blocks_and_block_says_why() {
    let view = View::default();
    // On start → fails: Failed → n ← n + 1.
    let mut run = Built::default()
        .kind("start", start())
        .kind("action", &Fails(true))
        .bump(2, 1)
        .flow(0, "then", 1)
        .flow(1, "failed", 2)
        .run();
    assert_eq!(run.advance(Some(&view), &Nothing, Instant::now()), &State::Finished);
    assert_eq!(n(&mut run), Some(json!(1)));

    let mut run = Built::default()
        .kind("start", start())
        .kind("action", &Fails(true))
        .flow(0, "then", 1)
        .run();
    run.advance(Some(&view), &Nothing, Instant::now());
    assert_eq!(blocked(&run), Some((Reason::Failed("lost".to_owned()), "action")));

    let mut run = Built::default()
        .kind("start", start())
        .kind("block", &Fails(false))
        .flow(0, "then", 1)
        .run();
    run.advance(Some(&view), &Nothing, Instant::now());
    assert_eq!(blocked(&run), Some((Reason::Said("stop here".to_owned()), "block")));
    let state = run.state().clone();
    assert_eq!(
        run.advance(Some(&view), &Nothing, Instant::now()),
        &state,
        "blocked stays so"
    );
}

#[test]
fn a_node_that_waits_wakes_on_the_clock_and_starts_over_after_an_absence() {
    let at = Instant::now();
    let view = View::default();
    let second = |milliseconds| at + Duration::from_millis(milliseconds);
    // On start → Repeat 2 → a second; completed → n + 1.
    let built = || {
        Built::default()
            .kind("start", start())
            .kind("repeat", &Repeat)
            .kind("second", &Second)
            .bump(3, 1)
            .flow(0, "then", 1)
            .input(1, "times", Source::Value(json!(2)))
            .flow(1, "body", 2)
            .flow(1, "completed", 3)
            .run()
    };
    let mut run = built();
    assert_eq!(run.advance(Some(&view), &Nothing, at), &State::Running);
    assert_eq!(run.wake(), Some(second(1_000)));
    assert_eq!(
        run.advance(Some(&view), &Nothing, second(999)),
        &State::Running,
        "not one tick early"
    );
    run.advance(Some(&view), &Nothing, second(1_000));
    assert_eq!(
        run.wake(),
        Some(second(2_000)),
        "the loop's second turn, inside its loop"
    );
    assert_eq!(run.advance(Some(&view), &Nothing, second(2_000)), &State::Finished);
    assert_eq!(n(&mut run), Some(json!(1)));

    // Away, nothing runs; back, the node at work starts over, its loop's count kept.
    let mut run = built();
    run.advance(Some(&view), &Nothing, at);
    run.advance(Some(&view), &Nothing, second(1_000));
    assert_eq!(run.advance(None, &Nothing, second(2_000)), &State::Running);
    assert_eq!(run.wake(), None, "away, it waits on nothing");
    run.advance(Some(&view), &Nothing, second(2_500));
    assert_eq!(run.wake(), Some(second(3_500)));
    assert_eq!(run.advance(Some(&view), &Nothing, second(3_500)), &State::Finished);

    // Paused, nothing runs either, and it starts over the same way.
    let mut run = built();
    run.advance(Some(&view), &Nothing, at);
    run.pause();
    assert_eq!(run.advance(Some(&view), &Nothing, second(1_000)), &State::Paused);
    run.resume();
    run.advance(Some(&view), &Nothing, second(800));
    assert_eq!(run.wake(), Some(second(1_800)), "started over, not ticked");
}
