//! v1's kinds run on a fixed clock against views written by hand: the waits, the actions' checks on the observed
//! state, the queries, the death, and D3B's own blueprints holding at their first action.

use std::collections::BTreeMap;
use std::time::{Duration, Instant};

use serde_json::{Value, json};

use super::actions::unseen;
use super::*;
use crate::bot::catalogue::KINDS;
use crate::bot::run::{Blocked, Fault, Knows, Reason, Run, State};
use crate::bot::{Blueprint, given, read};
use crate::game::{Fights, LifeNow, Pods, View, Who};

#[test]
fn every_kind_of_the_catalogue_runs() {
    for kind in KINDS {
        assert!(behaviour(kind.id).is_some(), "{}", kind.id);
    }
    assert!(behaviour("sit_until").is_none(), "a kind no catalogue holds");
}

/// Map 1 is in every zone, others in none; a profession harvests items 289 and 400.
struct Map1;

impl Knows for Map1 {
    fn in_zone(&self, _: &Value, map: i64) -> Option<bool> {
        Some(map == 1)
    }

    fn resources(&self, job: u64) -> Option<Vec<u64>> {
        (job == 28).then(|| vec![289, 400])
    }
}

/// On start → `node`, its Done (or Then) → Finish; its Failed wired to nothing.
fn one(kind: &str, inputs: &str, out: &str) -> Blueprint {
    read(&format!(
        r#"{{"schema": 1, "id": "one", "name": "One", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "it", "kind": "{kind}", "inputs": {inputs}}},
            {{"id": "end", "kind": "finish"}}],
  "wires": [{{"from": ["start", "then"], "to": ["it", "run"]}}, {{"from": ["it", "{out}"], "to": ["end", "run"]}}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}"))
}

fn run(blueprint: &Blueprint) -> Run {
    Run::new(blueprint, &BTreeMap::new(), &BTreeMap::new()).unwrap_or_else(|error| panic!("{error:?}"))
}

fn view(map: i64, life: i64, pods: i32) -> View {
    View {
        who: Some(Who {
            id: 7,
            name: "Lyra".to_owned(),
            level: 42,
            breed: 9,
            sex: None,
        }),
        map_id: Some(map),
        life: Some(LifeNow {
            life,
            max_life: 200,
            estimated: false,
        }),
        pods: Some(Pods {
            weight: pods,
            max_weight: 1_000,
        }),
        ..View::default()
    }
}

fn failed(run: &Run) -> Option<&Reason> {
    match run.state() {
        State::Blocked(Blocked { reason, .. }) => Some(reason),
        _ => None,
    }
}

#[test]
fn wait_wakes_after_its_minutes_and_zero_does_not_wait() {
    let at = Instant::now();
    let seen = view(1, 200, 0);
    let mut waits = run(&one("wait", r#"{"minutes": 2}"#, "then"));
    assert_eq!(waits.advance(Some(&seen), &Map1, at), &State::Running);
    assert_eq!(waits.wake(), Some(at + Duration::from_secs(120)));
    let early = at + Duration::from_millis(119_999);
    assert_eq!(waits.advance(Some(&seen), &Map1, early), &State::Running);
    assert_eq!(
        waits.advance(Some(&seen), &Map1, at + Duration::from_secs(120)),
        &State::Finished
    );
    assert_eq!(waits.wake(), None, "nothing waits once finished");

    // Neither paused nor blocked does it ask to be woken.
    let mut paused = run(&one("wait", r#"{"minutes": 2}"#, "then"));
    paused.advance(Some(&seen), &Map1, at);
    paused.pause();
    assert_eq!(paused.wake(), None);
    let mut dead = run(&one("wait", r#"{"minutes": 2}"#, "then"));
    dead.advance(Some(&seen), &Map1, at);
    let died = View {
        fights: Fights { won: 0, lost: 1 },
        ..seen.clone()
    };
    dead.advance(Some(&died), &Map1, at);
    assert_eq!((failed(&dead), dead.wake()), (Some(&Reason::Died), None));

    let mut now = run(&one("wait", r#"{"minutes": 0}"#, "then"));
    assert_eq!(now.advance(Some(&seen), &Map1, at), &State::Finished);
}

#[test]
fn wait_for_life_waits_on_the_life_seen() {
    let at = Instant::now();
    let mut waits = run(&one("wait_for_life", r#"{"life": 90}"#, "then"));
    assert_eq!(
        waits.advance(Some(&view(1, 179, 0)), &Map1, at),
        &State::Running,
        "89 %"
    );
    assert_eq!(
        waits.wake(),
        Some(at + Duration::from_secs(1)),
        "read again each second: it rises with no message"
    );
    assert_eq!(
        waits.advance(Some(&View::default()), &Map1, at),
        &State::Running,
        "no life seen yet"
    );
    assert_eq!(
        waits.advance(Some(&view(1, 180, 0)), &Map1, at),
        &State::Finished,
        "90 %"
    );
}

#[test]
fn an_action_waits_for_its_check_and_is_done_once_it_holds() {
    let at = Instant::now();
    let mut zone = run(&one("go_to_zone", r#"{"zone": {"sub_area": 445}}"#, "done"));
    assert_eq!(zone.advance(Some(&view(2, 200, 0)), &Map1, at), &State::Running);
    assert_eq!(
        zone.advance(Some(&view(1, 200, 0)), &Map1, at),
        &State::Finished,
        "the player walked there"
    );

    for kind in ["harvest_until", "hunt_until"] {
        let mut until = run(&one(kind, r#"{"zone": {"sub_area": 445}, "pods": 90}"#, "done"));
        assert_eq!(
            until.advance(Some(&view(1, 200, 899)), &Map1, at),
            &State::Running,
            "{kind}"
        );
        assert_eq!(
            until.advance(Some(&view(1, 200, 900)), &Map1, at),
            &State::Finished,
            "{kind}"
        );
    }

    let mut bank = run(&one("go_to_bank", r#"{"bank": {"map": 3}}"#, "done"));
    assert_eq!(bank.advance(Some(&view(1, 200, 0)), &Map1, at), &State::Running);
    assert_eq!(bank.advance(Some(&view(3, 200, 0)), &Map1, at), &State::Finished);
}

#[test]
fn an_action_whose_check_d3b_cannot_see_yet_fails_with_the_step_that_brings_it() {
    let at = Instant::now();
    let mut nearest = run(&one("go_to_bank", r#"{"bank": "nearest"}"#, "done"));
    nearest.advance(Some(&view(1, 200, 0)), &Map1, at);
    assert_eq!(failed(&nearest), Some(&Reason::Failed(unseen(4))));
    let mut deposit = run(&one("deposit_all_but", r#"{"keep": "none"}"#, "done"));
    deposit.advance(Some(&view(1, 200, 0)), &Map1, at);
    assert_eq!(failed(&deposit), Some(&Reason::Failed(unseen(5))));
    assert_eq!(unseen(5), "unseen_before_step_5");
}

#[test]
fn notify_tells_its_text_and_goes_on() {
    let at = Instant::now();
    let mut notify = run(&one("notify", r#"{"text": "Goal reached"}"#, "done"));
    assert_eq!(notify.advance(Some(&view(1, 200, 0)), &Map1, at), &State::Finished);
    let told: Vec<_> = notify
        .notices()
        .into_iter()
        .map(|notice| (notice.text, notice.at))
        .collect();
    assert_eq!(told, [("Goal reached".to_owned(), at)]);
    assert_eq!(notify.notices(), [], "told once");

    // A Notify wired back to itself: it runs without pause, and only the last notices wait.
    let looped = read(
        r#"{"schema": 1, "id": "l", "name": "L", "version": 1, "graphs": [{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "say", "kind": "notify", "inputs": {"text": "again"}}],
  "wires": [{"from": ["start", "then"], "to": ["say", "run"]}, {"from": ["say", "done"], "to": ["say", "run"]}]}]}"#,
    )
    .unwrap_or_else(|error| panic!("{error}"));
    let mut looped = run(&looped);
    looped.advance(Some(&view(1, 200, 0)), &Map1, at);
    assert_eq!(failed(&looped), Some(&Reason::WithoutPause));
    assert_eq!(looped.notices().len(), crate::bot::run::Notice::KEPT);
}

fn told(run: &mut Run) -> Vec<String> {
    run.notices().into_iter().map(|notice| notice.text).collect()
}

/// On start → a Sequence of three: 1 → Notify a; 2 → Wait 60 s → Notify b; 3 → Notify c → Finish.
fn sequence() -> Run {
    run(&read(
        r#"{"schema": 2, "id": "s", "name": "S", "version": 1, "graphs": [{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "seq", "kind": "sequence", "options": {"count": 3}},
            {"id": "a", "kind": "notify", "inputs": {"text": "a"}}, {"id": "w", "kind": "wait"},
            {"id": "b", "kind": "notify", "inputs": {"text": "b"}}, {"id": "c", "kind": "notify", "inputs": {"text": "c"}},
            {"id": "end", "kind": "finish"}],
  "wires": [{"from": ["start", "then"], "to": ["seq", "run"]}, {"from": ["seq", "then_1"], "to": ["a", "run"]},
            {"from": ["seq", "then_2"], "to": ["w", "run"]}, {"from": ["w", "then"], "to": ["b", "run"]},
            {"from": ["seq", "then_3"], "to": ["c", "run"]}, {"from": ["c", "done"], "to": ["end", "run"]}]}]}"#,
    )
    .unwrap_or_else(|error| panic!("{error}")))
}

#[test]
fn a_sequence_runs_each_output_once_the_one_before_ended_its_last_the_rest_of_the_flow() {
    let at = Instant::now();
    let seen = view(1, 200, 0);
    let mut seq = sequence();
    assert_eq!(seq.advance(Some(&seen), &Map1, at), &State::Running);
    assert_eq!(told(&mut seq), ["a"], "the second output waits before c");
    let later = at + Duration::from_secs(60);
    assert_eq!(
        seq.advance(Some(&seen), &Map1, later),
        &State::Finished,
        "the last output goes on to Finish"
    );
    assert_eq!(told(&mut seq), ["b", "c"]);
}

/// On start → a Sequence of two: 1 → Notify a; 2 → Wait 60 s → back into the Sequence.
#[test]
fn a_sequences_last_output_runs_in_its_place_so_a_flow_back_into_it_grows_no_stack() {
    let mut looped = run(&read(
        r#"{"schema": 2, "id": "s", "name": "S", "version": 1, "graphs": [{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "seq", "kind": "sequence"},
            {"id": "a", "kind": "notify", "inputs": {"text": "a"}}, {"id": "w", "kind": "wait"}],
  "wires": [{"from": ["start", "then"], "to": ["seq", "run"]}, {"from": ["seq", "then_1"], "to": ["a", "run"]},
            {"from": ["seq", "then_2"], "to": ["w", "run"]}, {"from": ["w", "then"], "to": ["seq", "run"]}]}]}"#,
    )
    .unwrap_or_else(|error| panic!("{error}")));
    let seen = view(1, 200, 0);
    let at = Instant::now();
    let mut depths = Vec::new();
    for turn in 0..3 {
        looped.advance(Some(&seen), &Map1, at + Duration::from_secs(60 * turn));
        let chains = looped
            .flows
            .last()
            .and_then(|flow| flow.last())
            .map(|call| call.chains.len());
        depths.push(chains);
    }
    assert_eq!(told(&mut looped), ["a", "a", "a"]);
    assert_eq!(
        depths,
        [Some(1); 3],
        "the wait in the last output is on the call's own chain"
    );
}

/// A blueprint of one event graph: On start `start`, then `nodes` and `wires` as the file writes them, and `variables`.
fn graph(variables: &str, nodes: &str, wires: &str) -> Blueprint {
    read(&format!(
        r#"{{"schema": 2, "id": "l", "name": "L", "version": 1, "variables": [{variables}],
  "graphs": [{{"id": "events", "name": "Events", "nodes": [{{"id": "start", "kind": "on_start"}}, {nodes}], "wires": [{wires}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}"))
}

fn said(text: &str) -> String {
    format!(r#"{{"id": "{text}", "kind": "notify", "inputs": {{"text": "{text}"}}}}"#)
}

fn wire(from: &str, pin: &str, to: &str, input: &str) -> String {
    format!(r#"{{"from": ["{from}", "{pin}"], "to": ["{to}", "{input}"]}}"#)
}

/// What a run of `blueprint` tells until it stops, and how it stopped.
fn told_by(blueprint: &Blueprint) -> (Vec<String>, State) {
    let mut run = run(blueprint);
    let state = run.advance(Some(&view(1, 200, 0)), &Map1, Instant::now()).clone();
    (told(&mut run), state)
}

/// Repeat `times` → its body a Switch on its index, case 2 → "two", Otherwise → "other"; Completed → "done".
fn repeat(times: i64) -> Blueprint {
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": {times}}}}},
           {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [2]}}}}, {}, {}, {}"#,
        said("two"),
        said("other"),
        said("done")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "sw", "run"),
        wire("rep", "index", "sw", "value"),
        wire("sw", "case_1", "two", "run"),
        wire("sw", "otherwise", "other", "run"),
        wire("rep", "completed", "done", "run"),
    ];
    graph("", &nodes, &wires.join(", "))
}

#[test]
fn repeat_runs_its_body_times_its_index_from_one_then_completed_and_none_for_zero() {
    let (said, state) = told_by(&repeat(3));
    assert_eq!(
        (said, state),
        (
            vec!["other".into(), "two".into(), "other".into(), "done".into()],
            State::Finished
        )
    );
    assert_eq!(told_by(&repeat(0)).0, ["done"]);
}

#[test]
fn leave_the_loop_ends_its_body_through_a_sequence_and_goes_on_out_of_completed() {
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": 5}}}}, {{"id": "seq", "kind": "sequence"}},
           {{"id": "out", "kind": "leave_loop"}}, {}, {}"#,
        said("a"),
        said("done")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "seq", "run"),
        wire("seq", "then_1", "a", "run"),
        wire("seq", "then_2", "out", "run"),
        wire("rep", "completed", "done", "run"),
    ];
    assert_eq!(told_by(&graph("", &nodes, &wires.join(", "))).0, ["a", "done"]);
}

#[test]
fn while_reads_its_condition_before_each_pass_and_one_that_never_waits_blocks() {
    // n from 0: while 2 ≥ n, n + 1 then "a"; three passes, then "done".
    let variables = r#"{"id": "n", "name": "N", "type": "number", "default": 0}"#;
    let nodes = format!(
        r#"{{"id": "w", "kind": "while"}}, {{"id": "ge", "kind": "greater_or_equal", "inputs": {{"a": 2}}}},
           {{"id": "get", "get": "n"}}, {{"id": "plus", "kind": "add", "inputs": {{"b": 1}}}}, {{"id": "set", "set": "n"}},
           {}, {}"#,
        said("a"),
        said("done")
    );
    let wires = [
        wire("start", "then", "w", "run"),
        wire("get", "value", "ge", "b"),
        wire("ge", "value", "w", "condition"),
        wire("w", "body", "set", "run"),
        wire("get", "value", "plus", "a"),
        wire("plus", "value", "set", "value"),
        wire("set", "then", "a", "run"),
        wire("w", "completed", "done", "run"),
    ];
    assert_eq!(
        told_by(&graph(variables, &nodes, &wires.join(", "))).0,
        ["a", "a", "a", "done"]
    );
    let looping = |condition: bool| {
        let nodes = format!(
            r#"{{"id": "w", "kind": "while", "inputs": {{"condition": {condition}}}}}, {}, {}"#,
            said("a"),
            said("done")
        );
        let wires = [
            wire("start", "then", "w", "run"),
            wire("w", "body", "a", "run"),
            wire("w", "completed", "done", "run"),
        ];
        graph("", &nodes, &wires.join(", "))
    };
    assert_eq!(told_by(&looping(false)).0, ["done"]);
    let mut forever = run(&looping(true));
    forever.advance(Some(&view(1, 200, 0)), &Map1, Instant::now());
    assert_eq!(failed(&forever), Some(&Reason::WithoutPause));
}

#[test]
fn leave_the_loop_ends_the_nearest_loop_only() {
    // Repeat 2 → Repeat 5 → Sequence: "in", then Leave; the inner Completed → "inner"; the outer → "done".
    let nodes = format!(
        r#"{{"id": "outer", "kind": "repeat", "inputs": {{"times": 2}}}}, {{"id": "inner", "kind": "repeat", "inputs": {{"times": 5}}}},
           {{"id": "seq", "kind": "sequence"}}, {{"id": "out", "kind": "leave_loop"}}, {}, {}, {}"#,
        said("in"),
        said("between"),
        said("done")
    );
    let wires = [
        wire("start", "then", "outer", "run"),
        wire("outer", "body", "inner", "run"),
        wire("inner", "body", "seq", "run"),
        wire("seq", "then_1", "in", "run"),
        wire("seq", "then_2", "out", "run"),
        wire("inner", "completed", "between", "run"),
        wire("outer", "completed", "done", "run"),
    ];
    let said_all = told_by(&graph("", &nodes, &wires.join(", "))).0;
    assert_eq!(said_all, ["in", "between", "in", "between", "done"]);
}

#[test]
fn repeat_reads_its_count_once_and_none_below_zero() {
    // n from 2: Repeat n, its body setting n to 10, then "a": two passes.
    let variables = r#"{"id": "n", "name": "N", "type": "number", "default": 2}"#;
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat"}}, {{"id": "get", "get": "n"}},
           {{"id": "set", "set": "n", "inputs": {{"value": 10}}}}, {}, {}"#,
        said("a"),
        said("done")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("get", "value", "rep", "times"),
        wire("rep", "body", "set", "run"),
        wire("set", "then", "a", "run"),
        wire("rep", "completed", "done", "run"),
    ];
    assert_eq!(
        told_by(&graph(variables, &nodes, &wires.join(", "))).0,
        ["a", "a", "done"]
    );
    assert_eq!(told_by(&repeat(-2)).0, ["done"]);
}

#[test]
fn an_interrupted_body_goes_on_with_its_index() {
    // Repeat 3 → Wait 60 s → a Switch on its index: 2 → "two", else "other".
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": 3}}}}, {{"id": "w", "kind": "wait"}},
           {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [2]}}}}, {}, {}"#,
        said("two"),
        said("other")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "w", "run"),
        wire("w", "then", "sw", "run"),
        wire("rep", "index", "sw", "value"),
        wire("sw", "case_1", "two", "run"),
        wire("sw", "otherwise", "other", "run"),
    ];
    let mut run = run(&graph("", &nodes, &wires.join(", ")));
    let (seen, at) = (view(1, 200, 0), Instant::now());
    let second = |seconds| at + Duration::from_secs(seconds);
    run.advance(Some(&seen), &Map1, at);
    run.advance(Some(&seen), &Map1, second(60));
    assert_eq!(
        run.advance(None, &Map1, second(90)),
        &State::Running,
        "away during the second pass"
    );
    run.advance(Some(&seen), &Map1, second(100));
    run.advance(Some(&seen), &Map1, second(160));
    assert_eq!(run.advance(Some(&seen), &Map1, second(220)), &State::Finished);
    assert_eq!(told(&mut run), ["other", "two", "other"]);
}

/// For each of the list variable `nums`, its body a Switch on its `by` output (`case` → "hit", Otherwise → "other")
/// then a Set of `nums` to an empty list; Completed → "done".
fn for_each(nums: &str, by: &str, case: i64) -> Blueprint {
    let variables = format!(r#"{{"id": "nums", "name": "Nums", "type": "number", "list": true, "default": {nums}}}"#);
    let nodes = format!(
        r#"{{"id": "each", "kind": "for_each", "options": {{"type": "number"}}}}, {{"id": "get", "get": "nums"}},
           {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [{case}]}}}},
           {{"id": "set", "set": "nums", "inputs": {{"value": []}}}}, {}, {}, {}"#,
        said("hit"),
        said("other"),
        said("done")
    );
    let wires = [
        wire("start", "then", "each", "run"),
        wire("get", "value", "each", "list"),
        wire("each", "body", "sw", "run"),
        wire("each", by, "sw", "value"),
        wire("sw", "case_1", "hit", "run"),
        wire("sw", "otherwise", "other", "run"),
        wire("hit", "done", "set", "run"),
        wire("other", "done", "set", "run"),
        wire("each", "completed", "done", "run"),
    ];
    graph(&variables, &nodes, &wires.join(", "))
}

#[test]
fn for_each_runs_its_body_per_element_of_the_list_read_once_as_it_enters() {
    assert_eq!(
        told_by(&for_each("[5, 6, 7]", "element", 6)).0,
        ["other", "hit", "other", "done"]
    );
    assert_eq!(
        told_by(&for_each("[5, 6, 7]", "index", 3)).0,
        ["other", "other", "hit", "done"],
        "indexes from 1"
    );
    assert_eq!(told_by(&for_each("[]", "element", 6)).0, ["done"]);
}

/// Repeat 4 → Sequence: 1 → At most 2 times → "a"; 2 → a Switch on the index, 3 → the At most's Reset when `reset`.
fn at_most(reset: bool) -> Blueprint {
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": 4}}}}, {{"id": "seq", "kind": "sequence"}},
           {{"id": "most", "kind": "do_n_times", "inputs": {{"times": 2}}}},
           {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [3]}}}}, {}"#,
        said("a")
    );
    let mut wires = vec![
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "seq", "run"),
        wire("seq", "then_1", "most", "run"),
        wire("most", "then", "a", "run"),
        wire("seq", "then_2", "sw", "run"),
        wire("rep", "index", "sw", "value"),
    ];
    if reset {
        wires.push(wire("sw", "case_1", "most", "reset"));
    }
    graph("", &nodes, &wires.join(", "))
}

#[test]
fn do_n_times_goes_on_its_first_times_until_its_reset_rearms_it() {
    assert_eq!(told_by(&at_most(false)).0, ["a", "a"]);
    assert_eq!(
        told_by(&at_most(true)).0,
        ["a", "a", "a"],
        "re-armed at the third pass, on at the fourth"
    );
}

#[test]
fn a_nodes_memory_lasts_the_run_in_a_function_called_again() {
    // Repeat 3 → a call of `f`, whose Do once says "f".
    let text = r#"{"schema": 2, "id": "m", "name": "M", "version": 1, "graphs": [
      {"id": "events", "name": "Events",
       "nodes": [{"id": "start", "kind": "on_start"}, {"id": "rep", "kind": "repeat"}, {"id": "c", "call": "f"}],
       "wires": [{"from": ["start", "then"], "to": ["rep", "run"]}, {"from": ["rep", "body"], "to": ["c", "run"]}]},
      {"id": "f", "name": "F", "function": {},
       "nodes": [{"id": "in", "kind": "entry"}, {"id": "once", "kind": "do_n_times"},
                 {"id": "say", "kind": "notify", "inputs": {"text": "f"}}],
       "wires": [{"from": ["in", "then"], "to": ["once", "run"]}, {"from": ["once", "then"], "to": ["say", "run"]}]}]}"#;
    let blueprint = read(text).unwrap_or_else(|error| panic!("{error}"));
    assert_eq!(told_by(&blueprint).0, ["f"]);
}

#[test]
fn alternate_goes_first_then_second_then_first() {
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat"}}, {{"id": "alt", "kind": "alternate"}}, {}, {}"#,
        said("first"),
        said("second")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "alt", "run"),
        wire("alt", "first", "first", "run"),
        wire("alt", "second", "second", "run"),
    ];
    assert_eq!(
        told_by(&graph("", &nodes, &wires.join(", "))).0,
        ["first", "second", "first"]
    );
}

/// Repeat `times` → Pick of the list variable `banks`, its element into a Switch: 2 → "two", else "other".
fn pick(banks: &str, times: i64) -> Blueprint {
    let variables =
        format!(r#"{{"id": "banks", "name": "Banks", "type": "number", "list": true, "default": {banks}}}"#);
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": {times}}}}}, {{"id": "get", "get": "banks"}},
           {{"id": "pick", "kind": "pick", "options": {{"type": "number"}}}},
           {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [2]}}}}, {}, {}"#,
        said("two"),
        said("other")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "pick", "run"),
        wire("get", "value", "pick", "list"),
        wire("pick", "then", "sw", "run"),
        wire("pick", "element", "sw", "value"),
        wire("sw", "case_1", "two", "run"),
        wire("sw", "otherwise", "other", "run"),
    ];
    graph(&variables, &nodes, &wires.join(", "))
}

#[test]
fn pick_takes_the_least_recently_picked_in_turn_and_an_empty_list_blocks() {
    assert_eq!(
        told_by(&pick("[1, 2, 3]", 5)).0,
        ["other", "two", "other", "other", "two"]
    );
    assert_eq!(
        told_by(&pick("[2, 2, 3]", 4)).0,
        ["two", "two", "other", "two"],
        "listed twice, two turns"
    );
    let mut empty = run(&pick("[]", 1));
    empty.advance(Some(&view(1, 200, 0)), &Map1, Instant::now());
    assert_eq!(
        failed(&empty),
        Some(&Reason::Fault(Fault::Empty { pin: "list".to_owned() }))
    );
}

/// On start → Wait until the life ≥ 90 %, at most `longest` seconds: Then → "then", Too long → "late".
fn wait_until(longest: i64) -> Blueprint {
    let nodes = format!(
        r#"{{"id": "until", "kind": "wait_until", "inputs": {{"longest": {longest}}}}}, {{"id": "life", "kind": "life_percent"}},
           {{"id": "ge", "kind": "greater_or_equal", "inputs": {{"b": 90}}}}, {}, {}"#,
        said("then"),
        said("late")
    );
    let wires = [
        wire("start", "then", "until", "run"),
        wire("life", "value", "ge", "a"),
        wire("ge", "value", "until", "condition"),
        wire("until", "then", "then", "run"),
        wire("until", "too_long", "late", "run"),
    ];
    graph("", &nodes, &wires.join(", "))
}

#[test]
fn wait_until_goes_on_once_its_condition_holds_read_each_second() {
    let at = Instant::now();
    let mut waits = run(&wait_until(0));
    assert_eq!(waits.advance(Some(&view(1, 179, 0)), &Map1, at), &State::Running);
    assert_eq!(
        waits.wake(),
        Some(at + Duration::from_secs(1)),
        "read again each second"
    );
    let later = at + Duration::from_secs(3_600);
    assert_eq!(
        waits.advance(Some(&view(1, 179, 0)), &Map1, later),
        &State::Running,
        "0: no limit"
    );
    assert_eq!(waits.advance(Some(&view(1, 180, 0)), &Map1, later), &State::Finished);
    assert_eq!(told(&mut waits), ["then"]);
}

#[test]
fn wait_until_counts_again_after_an_interruption_and_waits_on_a_value_not_seen_yet() {
    let at = Instant::now();
    let second = |seconds| at + Duration::from_secs(seconds);
    let mut waits = run(&wait_until(60));
    waits.advance(Some(&view(1, 100, 0)), &Map1, at);
    assert_eq!(waits.advance(None, &Map1, second(30)), &State::Running, "away");
    waits.advance(Some(&view(1, 100, 0)), &Map1, second(50));
    assert_eq!(waits.wake(), Some(second(51)));
    assert_eq!(
        waits.advance(Some(&view(1, 100, 0)), &Map1, second(60)),
        &State::Running,
        "counted from its return"
    );
    assert_eq!(
        waits.advance(Some(&view(1, 100, 0)), &Map1, second(110)),
        &State::Finished
    );
    assert_eq!(told(&mut waits), ["late"]);
    let mut unseen = run(&wait_until(0));
    assert_eq!(
        unseen.advance(Some(&View::default()), &Map1, at),
        &State::Running,
        "no life seen: it waits"
    );
    assert_eq!(unseen.wake(), Some(second(1)));
}

#[test]
fn wait_until_goes_out_of_too_long_after_its_longest_wait() {
    let at = Instant::now();
    let mut waits = run(&wait_until(60));
    waits.advance(Some(&view(1, 100, 0)), &Map1, at);
    assert_eq!(waits.wake(), Some(at + Duration::from_secs(1)));
    let almost = at + Duration::from_millis(59_500);
    waits.advance(Some(&view(1, 100, 0)), &Map1, almost);
    assert_eq!(
        waits.wake(),
        Some(at + Duration::from_secs(60)),
        "woken at its longest, not past it"
    );
    assert_eq!(
        waits.advance(Some(&view(1, 100, 0)), &Map1, at + Duration::from_secs(60)),
        &State::Finished
    );
    assert_eq!(told(&mut waits), ["late"]);
}

#[test]
fn block_blocks_the_character_with_its_reason() {
    let nodes = r#"{"id": "stop", "kind": "block", "inputs": {"reason": "Plus de place"}}"#;
    let mut blocks = run(&graph("", nodes, &wire("start", "then", "stop", "run")));
    blocks.advance(Some(&view(1, 200, 0)), &Map1, Instant::now());
    assert_eq!(failed(&blocks), Some(&Reason::Said("Plus de place".to_owned())));
}

#[test]
fn pause_and_notify_tells_its_text_and_resumes_after_itself() {
    let nodes = format!(
        r#"{{"id": "pause", "kind": "pause_notify", "inputs": {{"text": "Pause"}}}}, {}"#,
        said("after")
    );
    let wires = [
        wire("start", "then", "pause", "run"),
        wire("pause", "then", "after", "run"),
    ];
    let mut pauses = run(&graph("", &nodes, &wires.join(", ")));
    let (seen, at) = (view(1, 200, 0), Instant::now());
    assert_eq!(pauses.advance(Some(&seen), &Map1, at), &State::Paused);
    assert_eq!(told(&mut pauses), ["Pause"]);
    pauses.resume();
    assert_eq!(pauses.advance(Some(&seen), &Map1, at), &State::Finished);
    assert_eq!(told(&mut pauses), ["after"], "told once, and on past it");
}

#[test]
fn pause_ending_a_loops_body_resumes_into_the_next_pass_though_the_character_was_away() {
    // Repeat 3 → "x" → Pause "p", its Then wired to nothing; Completed → "done".
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": 3}}}}, {{"id": "pause", "kind": "pause_notify", "inputs": {{"text": "p"}}}},
           {}, {}"#,
        said("x"),
        said("done")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "x", "run"),
        wire("x", "done", "pause", "run"),
        wire("rep", "completed", "done", "run"),
    ];
    let mut pauses = run(&graph("", &nodes, &wires.join(", ")));
    let (seen, at) = (view(1, 200, 0), Instant::now());
    assert_eq!(pauses.advance(Some(&seen), &Map1, at), &State::Paused);
    for pass in 2..=3 {
        pauses.resume();
        pauses.advance(None, &Map1, at);
        assert_eq!(pauses.advance(Some(&seen), &Map1, at), &State::Paused, "pass {pass}");
    }
    pauses.resume();
    assert_eq!(pauses.advance(Some(&seen), &Map1, at), &State::Finished);
    assert_eq!(told(&mut pauses), ["x", "p", "x", "p", "x", "p", "done"]);
    // At the end of the flow, it pauses too, and finishes once resumed.
    let nodes = r#"{"id": "pause", "kind": "pause_notify", "inputs": {"text": "p"}}"#;
    let mut last = run(&graph("", nodes, &wire("start", "then", "pause", "run")));
    assert_eq!(last.advance(Some(&seen), &Map1, at), &State::Paused);
    last.resume();
    assert_eq!(last.advance(Some(&seen), &Map1, at), &State::Finished);
}

/// On start → Retry 3: its try a Switch on its index, case `case` → "second", Otherwise → a call of `f`, which says
/// "try" and fails "busy" (its call's Failed → "handled" when `handled`); Then → "ok", Failed → its reason said.
fn retry(case: i64, handled: bool) -> Blueprint {
    let handled_wire = if handled {
        r#", {"from": ["c", "failed"], "to": ["handled", "run"]}"#
    } else {
        ""
    };
    let text = format!(
        r#"{{"schema": 2, "id": "r", "name": "R", "version": 1, "graphs": [
      {{"id": "events", "name": "Events",
       "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "retry", "kind": "retry"}},
                 {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": [{case}]}}}}, {{"id": "c", "call": "f"}},
                 {}, {}, {}, {{"id": "gave", "kind": "notify"}}],
       "wires": [{{"from": ["start", "then"], "to": ["retry", "run"]}}, {{"from": ["retry", "try"], "to": ["sw", "run"]}},
                 {{"from": ["retry", "index"], "to": ["sw", "value"]}}, {{"from": ["sw", "case_1"], "to": ["second", "run"]}},
                 {{"from": ["sw", "otherwise"], "to": ["c", "run"]}}, {{"from": ["retry", "then"], "to": ["ok", "run"]}},
                 {{"from": ["retry", "failed"], "to": ["gave", "run"]}}, {{"from": ["retry", "reason"], "to": ["gave", "text"]}}{handled_wire}]}},
      {{"id": "f", "name": "F", "function": {{}},
       "nodes": [{{"id": "in", "kind": "entry"}}, {{"id": "say", "kind": "notify", "inputs": {{"text": "try"}}}},
                 {{"id": "no", "kind": "return_failed", "inputs": {{"reason": "busy"}}}}],
       "wires": [{{"from": ["in", "then"], "to": ["say", "run"]}}, {{"from": ["say", "done"], "to": ["no", "run"]}}]}}]}}"#,
        said("second"),
        said("ok"),
        said("handled")
    );
    read(&text).unwrap_or_else(|error| panic!("{error}"))
}

#[test]
fn retry_tries_again_on_an_unlinked_failed_and_goes_on_once_one_ends() {
    assert_eq!(
        told_by(&retry(2, false)).0,
        ["try", "second", "ok"],
        "its second try, index 2"
    );
}

#[test]
fn retry_goes_out_of_failed_with_the_last_reason_after_its_last_try() {
    assert_eq!(told_by(&retry(9, false)).0, ["try", "try", "try", "busy"]);
}

#[test]
fn a_failed_wired_inside_the_try_is_not_tried_again() {
    assert_eq!(told_by(&retry(9, true)).0, ["try", "handled", "ok"]);
}

/// Events of `nodes` and `wires` beside two functions: `f` says "try" and fails "busy"; `h` calls `f`, its Failed
/// linked to nothing.
fn with_failing(nodes: &str, wires: &str) -> Blueprint {
    read(&format!(
        r#"{{"schema": 2, "id": "t", "name": "T", "version": 1, "graphs": [
      {{"id": "events", "name": "Events", "nodes": [{{"id": "start", "kind": "on_start"}}, {nodes}], "wires": [{wires}]}},
      {{"id": "f", "name": "F", "function": {{}},
       "nodes": [{{"id": "in", "kind": "entry"}}, {{"id": "say", "kind": "notify", "inputs": {{"text": "try"}}}},
                 {{"id": "no", "kind": "return_failed", "inputs": {{"reason": "busy"}}}}],
       "wires": [{{"from": ["in", "then"], "to": ["say", "run"]}}, {{"from": ["say", "done"], "to": ["no", "run"]}}]}},
      {{"id": "h", "name": "H", "function": {{}},
       "nodes": [{{"id": "in", "kind": "entry"}}, {{"id": "cf", "call": "f"}}],
       "wires": [{{"from": ["in", "then"], "to": ["cf", "run"]}}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}"))
}

/// A Retry of `times`, its Failed's reason said by a Notify when `said`.
fn retry_node(id: &str, times: i64, said: bool) -> String {
    let notify = if said {
        format!(r#", {{"id": "{id}_gave", "kind": "notify"}}"#)
    } else {
        String::new()
    };
    format!(r#"{{"id": "{id}", "kind": "retry", "inputs": {{"times": {times}}}}}{notify}"#)
}

fn gave(id: &str) -> [String; 2] {
    [
        wire(id, "failed", &format!("{id}_gave"), "run"),
        wire(id, "reason", &format!("{id}_gave"), "text"),
    ]
}

#[test]
fn a_failure_inside_a_function_called_in_a_try_tries_it_again() {
    let nodes = format!(r#"{}, {{"id": "ch", "call": "h"}}"#, retry_node("r", 2, true));
    let mut wires = vec![wire("start", "then", "r", "run"), wire("r", "try", "ch", "run")];
    wires.extend(gave("r"));
    assert_eq!(
        told_by(&with_failing(&nodes, &wires.join(", "))).0,
        ["try", "try", "busy"]
    );
}

#[test]
fn a_retry_used_up_inside_another_tries_the_outer_again() {
    let nodes = format!(
        r#"{}, {}, {{"id": "cf", "call": "f"}}"#,
        retry_node("outer", 2, true),
        retry_node("inner", 2, false)
    );
    let mut wires = vec![
        wire("start", "then", "outer", "run"),
        wire("outer", "try", "inner", "run"),
        wire("inner", "try", "cf", "run"),
    ];
    wires.extend(gave("outer"));
    let said_all = told_by(&with_failing(&nodes, &wires.join(", "))).0;
    assert_eq!(
        said_all,
        ["try", "try", "try", "try", "busy"],
        "the inner's Failed unlinked"
    );
}

#[test]
fn a_failure_in_a_loop_in_a_try_drops_the_loop_which_starts_over() {
    let nodes = format!(
        r#"{}, {{"id": "rep", "kind": "repeat", "inputs": {{"times": 3}}}}, {}, {{"id": "cf", "call": "f"}}"#,
        retry_node("r", 2, true),
        said("pass")
    );
    let mut wires = vec![
        wire("start", "then", "r", "run"),
        wire("r", "try", "rep", "run"),
        wire("rep", "body", "pass", "run"),
        wire("pass", "done", "cf", "run"),
    ];
    wires.extend(gave("r"));
    let said_all = told_by(&with_failing(&nodes, &wires.join(", "))).0;
    assert_eq!(said_all, ["pass", "try", "pass", "try", "busy"]);
}

#[test]
fn leave_the_loop_in_a_try_in_a_loop_leaves_the_loop() {
    let nodes = format!(
        r#"{{"id": "rep", "kind": "repeat", "inputs": {{"times": 2}}}}, {}, {{"id": "seq", "kind": "sequence"}},
           {{"id": "out", "kind": "leave_loop"}}, {}, {}"#,
        retry_node("r", 3, false),
        said("a"),
        said("done")
    );
    let wires = [
        wire("start", "then", "rep", "run"),
        wire("rep", "body", "r", "run"),
        wire("r", "try", "seq", "run"),
        wire("seq", "then_1", "a", "run"),
        wire("seq", "then_2", "out", "run"),
        wire("rep", "completed", "done", "run"),
    ];
    assert_eq!(told_by(&with_failing(&nodes, &wires.join(", "))).0, ["a", "done"]);
}

#[test]
fn retry_of_no_tries_tries_once_and_an_interrupted_try_keeps_its_count() {
    let nodes = format!(r#"{}, {{"id": "cf", "call": "f"}}"#, retry_node("r", 0, true));
    let mut wires = vec![wire("start", "then", "r", "run"), wire("r", "try", "cf", "run")];
    wires.extend(gave("r"));
    assert_eq!(
        told_by(&with_failing(&nodes, &wires.join(", "))).0,
        ["try", "busy"],
        "one at least"
    );
    // Retry 2 → Wait 60 s → f: away during the second try's wait, it is still the second.
    let nodes = format!(
        r#"{}, {{"id": "w", "kind": "wait"}}, {{"id": "cf", "call": "f"}}"#,
        retry_node("r", 2, true)
    );
    let mut wires = vec![
        wire("start", "then", "r", "run"),
        wire("r", "try", "w", "run"),
        wire("w", "then", "cf", "run"),
    ];
    wires.extend(gave("r"));
    let mut run = run(&with_failing(&nodes, &wires.join(", ")));
    let (seen, at) = (view(1, 200, 0), Instant::now());
    let second = |seconds| at + Duration::from_secs(seconds);
    run.advance(Some(&seen), &Map1, at);
    run.advance(Some(&seen), &Map1, second(60));
    run.advance(None, &Map1, second(70));
    run.advance(Some(&seen), &Map1, second(80));
    assert_eq!(run.advance(Some(&seen), &Map1, second(140)), &State::Finished);
    assert_eq!(told(&mut run), ["try", "try", "busy"]);
}

/// On start → Switch on numbers of `cases`, its value `value`: case 1 → Notify one, case 2 → Notify two,
/// Otherwise → Notify other.
fn switch(cases: &str, value: &str) -> Run {
    run(&read(&format!(
        r#"{{"schema": 2, "id": "w", "name": "W", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}},
            {{"id": "sw", "kind": "switch", "options": {{"type": "number", "cases": {cases}}}, "inputs": {{"value": {value}}}}},
            {{"id": "one", "kind": "notify", "inputs": {{"text": "one"}}}}, {{"id": "two", "kind": "notify", "inputs": {{"text": "two"}}}},
            {{"id": "other", "kind": "notify", "inputs": {{"text": "other"}}}}],
  "wires": [{{"from": ["start", "then"], "to": ["sw", "run"]}}, {{"from": ["sw", "case_1"], "to": ["one", "run"]}},
            {{"from": ["sw", "case_2"], "to": ["two", "run"]}}, {{"from": ["sw", "otherwise"], "to": ["other", "run"]}}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}")))
}

#[test]
fn a_switch_follows_the_case_its_value_is_or_otherwise() {
    let at = Instant::now();
    for (cases, value, said) in [
        ("[24, 28]", "28", "two"),
        ("[24, 28]", "24", "one"),
        ("[24, 28]", "5", "other"),
    ] {
        let mut sw = switch(cases, value);
        assert_eq!(
            sw.advance(Some(&view(1, 200, 0)), &Map1, at),
            &State::Finished,
            "{value}"
        );
        assert_eq!(told(&mut sw), [said], "{value}");
    }
}

/// On start → If [`≥` of its `options`, `a` from N minutes of `minutes`, `b` typed] → True: Finish.
fn at_least(options: &str, minutes: i64, b: &str) -> Run {
    run(&read(&format!(
        r#"{{"schema": 2, "id": "q", "name": "Q", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "min", "kind": "minutes", "inputs": {{"minutes": {minutes}}}}},
            {{"id": "ge", "kind": "greater_or_equal", "options": {options}, "inputs": {{"b": {b}}}}},
            {{"id": "if", "kind": "if"}}, {{"id": "end", "kind": "finish"}}],
  "wires": [{{"from": ["start", "then"], "to": ["if", "run"]}}, {{"from": ["min", "value"], "to": ["ge", "a"]}},
            {{"from": ["ge", "value"], "to": ["if", "condition"]}}, {{"from": ["if", "true"], "to": ["end", "run"]}}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}")))
}

#[test]
fn n_minutes_are_their_seconds_and_a_comparison_reads_durations_by_their_value() {
    let at = Instant::now();
    let condition = |mut run: Run| {
        run.advance(Some(&View::default()), &Map1, at);
        run.trace().iter().map(|it| it.value.clone()).next()
    };
    let duration = r#"{"type": "duration"}"#;
    assert_eq!(condition(at_least(duration, 2, "120")), Some(json!(true)));
    assert_eq!(condition(at_least(duration, 2, "121")), Some(json!(false)));
    let mut negative = at_least(duration, -1, "0");
    negative.advance(Some(&View::default()), &Map1, at);
    let not = Fault::NotOfType {
        pin: "minutes".to_owned(),
    };
    assert_eq!(failed(&negative), Some(&Reason::Fault(not)));
    let mut long = at_least(duration, i64::MAX, "0");
    long.advance(Some(&View::default()), &Map1, at);
    assert_eq!(failed(&long), Some(&Reason::Fault(Fault::TooLarge)));
}

#[test]
fn a_comparison_of_times_reads_them_in_the_days_order() {
    let at = Instant::now();
    let times = |a: &str, b: &str| {
        let text = format!(
            r#"{{"schema": 2, "id": "q", "name": "Q", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}},
            {{"id": "ge", "kind": "greater_or_equal", "options": {{"type": "time"}}, "inputs": {{"a": "{a}", "b": "{b}"}}}},
            {{"id": "if", "kind": "if"}}],
  "wires": [{{"from": ["start", "then"], "to": ["if", "run"]}}, {{"from": ["ge", "value"], "to": ["if", "condition"]}}]}}]}}"#
        );
        let mut run = run(&read(&text).unwrap_or_else(|error| panic!("{error}")));
        run.advance(Some(&View::default()), &Map1, at);
        run.trace().iter().map(|it| it.value.clone()).next()
    };
    assert_eq!(times("09:30", "08:45"), Some(json!(true)));
    assert_eq!(times("08:45", "09:30"), Some(json!(false)));
    assert_eq!(times("23:00", "23:00"), Some(json!(true)));
}

#[test]
fn a_wait_whose_minutes_a_wire_fed_in_schema_1_waits_them_through_its_minutes_node() {
    let at = Instant::now();
    let text = r#"{"schema": 1, "id": "w", "name": "W", "version": 1,
      "variables": [{"id": "pause", "name": "Pause", "type": "number", "default": 2}],
      "graphs": [{"id": "events", "name": "Events",
        "nodes": [{"id": "start", "kind": "on_start"}, {"id": "pause", "get": "pause"}, {"id": "it", "kind": "wait"},
                  {"id": "end", "kind": "finish"}],
        "wires": [{"from": ["start", "then"], "to": ["it", "run"]}, {"from": ["pause", "value"], "to": ["it", "minutes"]},
                  {"from": ["it", "then"], "to": ["end", "run"]}]}]}"#;
    let mut waits = run(&read(text).unwrap_or_else(|error| panic!("{error}")));
    let seen = view(1, 200, 0);
    assert_eq!(waits.advance(Some(&seen), &Map1, at), &State::Running);
    assert_eq!(waits.wake(), Some(at + Duration::from_secs(120)));
}

/// On start → If [`query` ≥ `b`] → True: Finish; False wired to nothing.
fn compared(query: &str, b: i64) -> Blueprint {
    read(&format!(
        r#"{{"schema": 1, "id": "q", "name": "Q", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "query", "kind": "{query}"}},
            {{"id": "at_least", "kind": "at_least", "inputs": {{"b": {b}}}}}, {{"id": "if", "kind": "if"}},
            {{"id": "end", "kind": "finish"}}],
  "wires": [{{"from": ["start", "then"], "to": ["if", "run"]}}, {{"from": ["query", "value"], "to": ["at_least", "a"]}},
            {{"from": ["at_least", "value"], "to": ["if", "condition"]}}, {{"from": ["if", "true"], "to": ["end", "run"]}}]}}]}}"#
    ))
    .unwrap_or_else(|error| panic!("{error}"))
}

#[test]
fn the_queries_read_the_view_when_used_and_block_on_what_is_not_seen() {
    let at = Instant::now();
    let seen = view(1, 101, 250);
    // Life 101 of 200 is 50 %, pods 250 of 1,000 are 25 %, the level 42.
    for (query, value) in [("life_percent", 50), ("pods_percent", 25), ("level", 42)] {
        let mut true_at = run(&compared(query, value));
        let mut false_above = run(&compared(query, value + 1));
        assert_eq!(true_at.advance(Some(&seen), &Map1, at), &State::Finished, "{query}");
        assert_eq!(false_above.advance(Some(&seen), &Map1, at), &State::Finished, "{query}");
        let read: Vec<_> = true_at.trace().iter().map(|it| it.value.clone()).collect();
        assert_eq!(read, [json!(true)], "{query}: the condition used is traced");
    }
    let mut unseen = run(&compared("life_percent", 50));
    unseen.advance(Some(&View::default()), &Map1, at);
    let fault = Fault::NotSeen {
        what: "life".to_owned(),
    };
    assert_eq!(failed(&unseen), Some(&Reason::Fault(fault)));
}

#[test]
fn a_professions_resources_come_from_what_the_run_knows() {
    let blueprint = read(
        r#"{"schema": 1, "id": "r", "name": "R", "version": 1,
  "variables": [{"id": "what", "name": "What", "type": "items", "default": "none", "memory": true}],
  "graphs": [{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "resources", "kind": "job_resources", "inputs": {"job": 28}},
            {"id": "set", "set": "what"}],
  "wires": [{"from": ["start", "then"], "to": ["set", "run"]}, {"from": ["resources", "value"], "to": ["set", "value"]}]}]}"#,
    )
    .unwrap_or_else(|error| panic!("{error}"));
    let mut known = run(&blueprint);
    known.advance(Some(&view(1, 200, 0)), &Map1, Instant::now());
    assert_eq!(known.memory().0.get("what"), Some(&json!([289, 400])));
    let mut unknown = run(&blueprint);
    unknown.advance(Some(&view(1, 200, 0)), &super::super::knows::Nothing, Instant::now());
    let fault = Fault::NotSeen {
        what: "resources".to_owned(),
    };
    assert_eq!(failed(&unknown), Some(&Reason::Fault(fault)));
}

#[test]
fn a_fight_lost_after_the_run_first_saw_the_character_blocks_it_dead() {
    let at = Instant::now();
    let lost = |lost| View {
        fights: Fights { won: 3, lost },
        ..view(2, 200, 0)
    };
    let mut waiting = run(&one("go_to_zone", r#"{"zone": {"sub_area": 445}}"#, "done"));
    // Two fights lost before the run started are not its death.
    assert_eq!(waiting.advance(Some(&lost(2)), &Map1, at), &State::Running);
    assert_eq!(waiting.advance(Some(&lost(2)), &Map1, at), &State::Running);
    waiting.advance(Some(&lost(3)), &Map1, at);
    assert_eq!(failed(&waiting), Some(&Reason::Died));

    // Back from a new connection, or chosen again, the count starts from 0: the next fight lost is a death too.
    let mut back = run(&one("go_to_zone", r#"{"zone": {"sub_area": 445}}"#, "done"));
    back.advance(Some(&lost(1)), &Map1, at);
    assert_eq!(back.advance(None, &Map1, at), &State::Running);
    assert_eq!(back.advance(Some(&lost(0)), &Map1, at), &State::Running);
    back.advance(Some(&lost(1)), &Map1, at);
    assert_eq!(failed(&back), Some(&Reason::Died));
}

#[test]
fn d3bs_blueprints_run_and_hold_at_their_first_action() {
    let at = Instant::now();
    let settings = BTreeMap::from([
        ("zone".to_owned(), json!({"sub_area": 445})),
        ("job".to_owned(), json!(28)),
    ]);
    let blueprints = given().unwrap_or_else(|error| panic!("{error}"));
    assert_eq!(blueprints.len(), 3, "the three of v1");
    for blueprint in &blueprints {
        let run = Run::new(blueprint, &settings, &BTreeMap::new());
        let mut run = run.unwrap_or_else(|error| panic!("{}: {error:?}", blueprint.id.0));
        assert_eq!(
            run.advance(Some(&view(2, 200, 0)), &Map1, at),
            &State::Running,
            "{}",
            blueprint.id.0
        );
        let zone = run.trace().iter().any(|it| it.value == json!({"sub_area": 445}));
        assert!(zone, "{}: the Zone it goes to is traced", blueprint.id.0);
    }
}
