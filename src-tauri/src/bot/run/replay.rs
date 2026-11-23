//! The runtime on the owner's game captures (`docs/bot.md`, "The runtime"): a blueprint that waits for its life, waits a
//! minute, reads its life and loops, one of v2's flow (a Retry around a Repeat of Wait until, a Switch), and one of its
//! values (AND, NOT, a Text, a draw, a List), each run
//! on each connection's character as an actor would, advanced at each message and when it asked to wake. A capture holds the account's ticket: it prints counts only. Ignored unless
//! `D3B_CAPTURES` names their folder.

use std::collections::BTreeMap;
use std::time::Instant;

use super::knows::Nothing;
use super::{Reason, Run, State};
use crate::bot::read;
use crate::game::Character;
use crate::protocol::Message;
use crate::protocol::replay::{decoded, folder};

/// On start → Wait for life 100 % → Wait 1 min → If life % ≥ 50: n ← n + 1, else Notify; then again.
const LOOP: &str = r#"{"schema": 1, "id": "loop", "name": "Loop", "version": 1,
  "variables": [{"id": "n", "name": "N", "type": "number", "default": 0, "memory": true}],
  "graphs": [{"id": "events", "name": "Events",
    "nodes": [{"id": "start", "kind": "on_start"}, {"id": "full", "kind": "wait_for_life", "inputs": {"life": 100}},
              {"id": "minute", "kind": "wait", "inputs": {"minutes": 1}}, {"id": "life", "kind": "life_percent"},
              {"id": "half", "kind": "at_least", "inputs": {"b": 50}}, {"id": "if", "kind": "if"},
              {"id": "n", "get": "n"}, {"id": "add", "kind": "add", "inputs": {"b": 1}}, {"id": "bump", "set": "n"},
              {"id": "low", "kind": "notify", "inputs": {"text": "low"}}],
    "wires": [{"from": ["start", "then"], "to": ["full", "run"]}, {"from": ["full", "then"], "to": ["minute", "run"]},
              {"from": ["minute", "then"], "to": ["if", "run"]}, {"from": ["life", "value"], "to": ["half", "a"]},
              {"from": ["half", "value"], "to": ["if", "condition"]}, {"from": ["if", "true"], "to": ["bump", "run"]},
              {"from": ["n", "value"], "to": ["add", "a"]}, {"from": ["add", "value"], "to": ["bump", "value"]},
              {"from": ["bump", "then"], "to": ["full", "run"]}, {"from": ["if", "false"], "to": ["low", "run"]},
              {"from": ["low", "done"], "to": ["full", "run"]}]}]}"#;

/// What the replay counted, over every connection.
#[derive(Debug, Default)]
struct Counts {
    /// Connections whose character was chosen, and their runs' loops.
    runs: u32,
    loops: u64,
    /// Runs blocked by a death, those at a fight's end lost, and those blocked otherwise.
    died: u32,
    died_at_the_fights_end: u32,
    blocked_otherwise: u32,
    /// Connections where the character lost a fight.
    with_a_loss: u32,
    /// Times the run asked to be woken by the clock and was (each minute's end, and each second while waiting for its
    /// life), and the notices: of a life below half after its wait, or the values' Text.
    wakes: u32,
    lows: usize,
}

/// v2's flow (`docs/bot.md`, "The flow's kinds (v2.3)"): On start → Retry 3 → its try a Repeat 3 of Wait until the
/// life ≥ 90 % (at most 1 min; Too long → Notify), then a Switch on the level (200 → Notify, else n ← n + 1); Retry's
/// Then → Wait 1 min → Retry again. Nothing in it fails: it measures the flow never running without pause.
const FLOW: &str = r#"{"schema": 2, "id": "flow", "name": "Flow", "version": 1,
  "variables": [{"id": "n", "name": "N", "type": "number", "default": 0, "memory": true}],
  "graphs": [{"id": "events", "name": "Events",
    "nodes": [{"id": "start", "kind": "on_start"}, {"id": "retry", "kind": "retry"},
              {"id": "rep", "kind": "repeat"}, {"id": "until", "kind": "wait_until", "inputs": {"longest": 60}},
              {"id": "life", "kind": "life_percent"}, {"id": "ge", "kind": "greater_or_equal", "inputs": {"b": 90}},
              {"id": "slow", "kind": "notify", "inputs": {"text": "slow"}},
              {"id": "sw", "kind": "switch", "options": {"type": "number", "cases": [200]}}, {"id": "level", "kind": "level"},
              {"id": "top", "kind": "notify", "inputs": {"text": "top"}},
              {"id": "n", "get": "n"}, {"id": "add", "kind": "add", "inputs": {"b": 1}}, {"id": "bump", "set": "n"},
              {"id": "minute", "kind": "wait", "inputs": {"duration": 60}}],
    "wires": [{"from": ["start", "then"], "to": ["retry", "run"]}, {"from": ["retry", "try"], "to": ["rep", "run"]},
              {"from": ["rep", "body"], "to": ["until", "run"]}, {"from": ["life", "value"], "to": ["ge", "a"]},
              {"from": ["ge", "value"], "to": ["until", "condition"]}, {"from": ["until", "too_long"], "to": ["slow", "run"]},
              {"from": ["until", "then"], "to": ["sw", "run"]}, {"from": ["level", "value"], "to": ["sw", "value"]},
              {"from": ["sw", "case_1"], "to": ["top", "run"]}, {"from": ["sw", "otherwise"], "to": ["bump", "run"]},
              {"from": ["n", "value"], "to": ["add", "a"]}, {"from": ["add", "value"], "to": ["bump", "value"]},
              {"from": ["retry", "then"], "to": ["minute", "run"]}, {"from": ["minute", "then"], "to": ["retry", "run"]}]}]}"#;

/// v2's values (`docs/bot.md`, "The values (v2.4)"): On start → Wait until life ≥ 50 % AND NOT level < 1 (at most
/// 1 min) → Notify a Text "{v} % at level {l}" → n ← clamp(n + a random 1 to 3, 0, 1000) → Wait N seconds, element
/// "chance 50 % ? 1 : 2" of a List [30, 60] → again. Nothing in it faults: it measures the values never blocking.
const VALUES: &str = r#"{"schema": 2, "id": "values", "name": "Values", "version": 1,
  "variables": [{"id": "n", "name": "N", "type": "number", "default": 0, "memory": true}],
  "graphs": [{"id": "events", "name": "Events",
    "nodes": [{"id": "start", "kind": "on_start"}, {"id": "until", "kind": "wait_until", "inputs": {"longest": 60}},
              {"id": "life", "kind": "life_percent"}, {"id": "ge", "kind": "greater_or_equal", "inputs": {"b": 50}},
              {"id": "level", "kind": "level"}, {"id": "lt", "kind": "less", "inputs": {"b": 1}},
              {"id": "not", "kind": "not"}, {"id": "and", "kind": "and"},
              {"id": "said", "kind": "text", "options": {"text": "{v} % at level {l}"}},
              {"id": "tell", "kind": "notify"},
              {"id": "n", "get": "n"}, {"id": "draw", "kind": "random_number", "inputs": {"low": 1, "high": 3}},
              {"id": "add", "kind": "add"}, {"id": "clamp", "kind": "clamp", "inputs": {"low": 0, "high": 1000}},
              {"id": "bump", "set": "n"},
              {"id": "list", "kind": "make_list", "options": {"type": "number", "count": 2}, "inputs": {"in_1": 30, "in_2": 60}},
              {"id": "coin", "kind": "chance"}, {"id": "pick", "kind": "choose", "options": {"type": "number"},
               "inputs": {"if_true": 1, "if_false": 2}},
              {"id": "at", "kind": "element_at", "options": {"type": "number"}}, {"id": "secs", "kind": "seconds"}, {"id": "pause", "kind": "wait"}],
    "wires": [{"from": ["start", "then"], "to": ["until", "run"]}, {"from": ["life", "value"], "to": ["ge", "a"]},
              {"from": ["level", "value"], "to": ["lt", "a"]}, {"from": ["lt", "value"], "to": ["not", "value"]},
              {"from": ["ge", "value"], "to": ["and", "in_1"]}, {"from": ["not", "value"], "to": ["and", "in_2"]},
              {"from": ["and", "value"], "to": ["until", "condition"]}, {"from": ["until", "then"], "to": ["tell", "run"]},
              {"from": ["until", "too_long"], "to": ["tell", "run"]},
              {"from": ["life", "value"], "to": ["said", "v"]}, {"from": ["level", "value"], "to": ["said", "l"]},
              {"from": ["said", "value"], "to": ["tell", "text"]}, {"from": ["tell", "done"], "to": ["bump", "run"]},
              {"from": ["n", "value"], "to": ["add", "a"]}, {"from": ["draw", "value"], "to": ["add", "b"]},
              {"from": ["add", "value"], "to": ["clamp", "value"]}, {"from": ["clamp", "value"], "to": ["bump", "value"]},
              {"from": ["coin", "value"], "to": ["pick", "condition"]}, {"from": ["list", "value"], "to": ["at", "list"]},
              {"from": ["pick", "value"], "to": ["at", "index"]}, {"from": ["at", "element"], "to": ["secs", "seconds"]},
              {"from": ["secs", "value"], "to": ["pause", "duration"]}, {"from": ["bump", "then"], "to": ["pause", "run"]},
              {"from": ["pause", "then"], "to": ["until", "run"]}]}]}"#;

#[test]
fn v2s_flow_and_values_replayed_on_the_captures_compile() {
    for text in [FLOW, VALUES] {
        let blueprint = read(text).unwrap_or_else(|error| panic!("{error}"));
        let run = Run::new(&blueprint, &BTreeMap::new(), &BTreeMap::new());
        assert!(run.is_ok(), "{:?}", run.err());
    }
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_run_a_blueprint_on_each_character() {
    let counts = replayed(LOOP);
    eprintln!("{counts:?}");
    assert!(
        counts.runs > 0 && counts.loops > 0 && counts.wakes > 0,
        "nothing ran: {counts:?}"
    );
    assert_eq!(counts.blocked_otherwise, 0, "blocked by something else than a death");
    assert_eq!(
        counts.died, counts.with_a_loss,
        "one death blocks each run whose character lost"
    );
    assert_eq!(counts.died_at_the_fights_end, counts.died, "blocked at the fight's end");
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_run_v2s_flow_without_blocking_but_on_a_death() {
    let counts = replayed(FLOW);
    eprintln!("{counts:?}");
    assert!(counts.runs > 0 && counts.wakes > 0, "nothing ran: {counts:?}");
    assert_eq!(
        counts.blocked_otherwise, 0,
        "never \"runs without pause\", nor another block"
    );
    assert_eq!(counts.died, counts.with_a_loss, "only a death blocks");
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_run_v2s_values_without_blocking_but_on_a_death() {
    let counts = replayed(VALUES);
    eprintln!("{counts:?}");
    assert!(
        counts.runs > 0 && counts.wakes > 0 && counts.lows > 0,
        "nothing ran: {counts:?}"
    );
    assert_eq!(counts.blocked_otherwise, 0, "no value faults, nor another block");
    assert_eq!(counts.died, counts.with_a_loss, "only a death blocks");
}

/// `blueprint` run on each connection's character of the owner's captures, as an actor would.
fn replayed(blueprint: &str) -> Counts {
    let blueprint = read(blueprint).unwrap_or_else(|error| panic!("{error}"));
    let start = Instant::now();
    let mut counts = Counts::default();
    decoded(&folder(), |messages| {
        let mut character = Character::default();
        let mut run =
            Run::new(&blueprint, &BTreeMap::new(), &BTreeMap::new()).unwrap_or_else(|error| panic!("{error}"));
        let mut chosen = false;
        for (at, message) in messages {
            let now = start + *at;
            // The actor's timer: a wait asked to be woken before this message came.
            while let Some(wake) = run.wake().filter(|wake| *wake <= now) {
                run.advance(Some(&character.view(wake, 0)), &Nothing, wake);
                counts.wakes += 1;
                if run.wake() == Some(wake) {
                    break;
                }
            }
            let Some(message) = message else { continue };
            character.apply(message, now, 0);
            let view = character.view(now, 0);
            if view.who.is_none() {
                continue;
            }
            chosen = true;
            let before = run.state().clone();
            let after = run.advance(Some(&view), &Nothing, now).clone();
            if before != after
                && let State::Blocked(blocked) = &after
                && blocked.reason == Reason::Died
            {
                counts.died_at_the_fights_end += u32::from(matches!(message, Message::FightEndEvent(_)));
            }
        }
        // How each run ended, wherever it stopped: at a message or woken by the clock.
        match run.state() {
            State::Blocked(blocked) if blocked.reason == Reason::Died => counts.died += 1,
            State::Blocked(blocked) => {
                counts.blocked_otherwise += 1;
                eprintln!("blocked: {:?}", blocked.reason);
            }
            _ => {}
        }
        counts.lows += run.notices().len();
        if chosen {
            counts.runs += 1;
            counts.loops += run
                .memory()
                .0
                .get("n")
                .and_then(serde_json::Value::as_u64)
                .unwrap_or_default();
            counts.with_a_loss += u32::from(character.view(start, 0).fights.lost > 0);
        }
    });
    counts
}
