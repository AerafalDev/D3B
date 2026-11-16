//! The runtime on the owner's game captures (`docs/bot.md`, "The runtime"): a blueprint that waits for its life, waits a
//! minute, reads its life and loops, run on each connection's character as an actor would, advanced at each message
//! and when it asked to wake. A capture holds the account's ticket: it prints counts only. Ignored unless
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
    /// Times the run asked to be woken by the clock and was, and the notices of a life below half after its wait.
    wakes: u32,
    lows: usize,
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_run_a_blueprint_on_each_character() {
    let blueprint = read(LOOP).unwrap_or_else(|error| panic!("{error}"));
    let start = Instant::now();
    let mut counts = Counts::default();
    decoded(&folder(), |messages| {
        let mut character = Character::default();
        let mut run = Run::new(&blueprint, &BTreeMap::new(), &BTreeMap::new()).unwrap_or_else(|error| panic!("{error}"));
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
            {
                if blocked.reason == Reason::Died {
                    counts.died += 1;
                    counts.died_at_the_fights_end += u32::from(matches!(message, Message::FightEndEvent(_)));
                } else {
                    counts.blocked_otherwise += 1;
                    eprintln!("blocked: {:?}", blocked.reason);
                }
            }
        }
        counts.lows += run.notices().len();
        if chosen {
            counts.runs += 1;
            counts.loops += run.memory().0.get("n").and_then(serde_json::Value::as_u64).unwrap_or_default();
            counts.with_a_loss += u32::from(character.view(start, 0).fights.lost > 0);
        }
    });
    eprintln!("{counts:?}");
    assert!(counts.runs > 0 && counts.loops > 0 && counts.wakes > 0, "nothing ran: {counts:?}");
    // Each turn of the loop waits one minute on the clock, once.
    let turns = counts.loops + counts.lows as u64;
    assert!(u64::from(counts.wakes) <= turns + u64::from(counts.runs), "woken more than once a turn: {counts:?}");
    assert_eq!(counts.blocked_otherwise, 0, "blocked by something else than a death");
    assert_eq!(counts.died, counts.with_a_loss, "one death blocks each run whose character lost");
    assert_eq!(counts.died_at_the_fights_end, counts.died, "blocked at the fight's end");
}
