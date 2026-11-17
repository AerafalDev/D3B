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
