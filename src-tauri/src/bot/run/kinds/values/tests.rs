//! The values read by an If on a run of their own: each comparison at its edges, and AND, OR and Choose never reading
//! the side they do not take, whose life is not seen yet (it would block the character).

use std::collections::BTreeMap;
use std::time::Instant;

use crate::bot::read;
use crate::bot::run::knows::Nothing;
use crate::bot::run::{Blocked, Fault, Reason, Run, State};
use crate::game::View;

/// On start → If, its condition the output `value` of `node` (with `more` nodes and `wires` beside): true → "yes",
/// false → "no"; what the run says, or why it blocked.
fn decide(node: &str, more: &str, wires: &str) -> Result<String, Reason> {
    let text = format!(
        r#"{{"schema": 2, "id": "v", "name": "V", "version": 1, "graphs": [{{"id": "events", "name": "Events",
  "nodes": [{{"id": "start", "kind": "on_start"}}, {{"id": "if", "kind": "if"}}, {node}{more},
            {{"id": "yes", "kind": "notify", "inputs": {{"text": "yes"}}}}, {{"id": "no", "kind": "notify", "inputs": {{"text": "no"}}}}],
  "wires": [{{"from": ["start", "then"], "to": ["if", "run"]}}, {{"from": ["it", "value"], "to": ["if", "condition"]}},
            {{"from": ["if", "true"], "to": ["yes", "run"]}}, {{"from": ["if", "false"], "to": ["no", "run"]}}{wires}]}}]}}"#
    );
    let blueprint = read(&text).unwrap_or_else(|error| panic!("{error}"));
    let mut run = Run::new(&blueprint, &BTreeMap::new(), &BTreeMap::new()).unwrap_or_else(|error| panic!("{error:?}"));
    match run.advance(Some(&View::default()), &Nothing, Instant::now()).clone() {
        State::Blocked(Blocked { reason, .. }) => Err(reason),
        _ => Ok(run.notices().into_iter().map(|notice| notice.text).collect()),
    }
}

/// A node `it` of `kind`, its wildcards of `of`, given `inputs`.
fn it(kind: &str, of: &str, inputs: &str) -> String {
    format!(r#"{{"id": "it", "kind": "{kind}", "options": {{"type": "{of}"}}, "inputs": {inputs}}}"#)
}

fn yes(node: &str) -> bool {
    match decide(node, "", "") {
        Ok(said) => said == "yes",
        Err(reason) => panic!("{reason:?}"),
    }
}

#[test]
fn the_comparisons_read_numbers_durations_and_times_in_their_order() {
    for (kind, a, b, holds) in [
        ("less", "3", "5", true),
        ("less", "5", "5", false),
        ("less_or_equal", "5", "5", true),
        ("less_or_equal", "3", "5", true),
        ("less_or_equal", "6", "5", false),
        ("greater", "6", "5", true),
        ("greater", "5", "5", false),
        ("greater_or_equal", "5", "5", true),
        ("greater_or_equal", "6", "5", true),
        ("greater_or_equal", "4", "5", false),
        ("less", r#""08:00""#, r#""23:00""#, true),
    ] {
        let of = if a.starts_with('"') { "time" } else { "number" };
        let node = it(kind, of, &format!(r#"{{"a": {a}, "b": {b}}}"#));
        assert_eq!(yes(&node), holds, "{a} {kind} {b}");
    }
}

#[test]
fn equal_takes_any_type_and_not_equal_is_its_opposite() {
    let zones = |a: &str, b: &str| format!(r#"{{"a": {{"zone": "{a}"}}, "b": {{"zone": "{b}"}}}}"#);
    assert!(yes(&it("equal", "zone", &zones("forest", "forest"))));
    assert!(!yes(&it("equal", "zone", &zones("forest", "fields"))));
    assert!(yes(&it("not_equal", "zone", &zones("forest", "fields"))));
    assert!(yes(&it("equal", "number", r#"{"a": 5, "b": 5}"#)));
    let near = r#"{"a": 9007199254740993, "b": 9007199254740992}"#;
    assert!(
        !yes(&it("equal", "number", near)),
        "whole numbers exactly, past a float's"
    );
}

#[test]
fn a_value_of_no_order_blocks_on_its_own_pin() {
    // A duration past a whole number's largest, 2e17 minutes: no order.
    let huge = r#", {"id": "min", "kind": "minutes", "inputs": {"minutes": 200000000000000000}}"#;
    let wired = |pin: &str| format!(r#", {{"from": ["min", "value"], "to": ["it", "{pin}"]}}"#);
    let less = it("less", "duration", r#"{"b": 5}"#);
    assert_eq!(
        decide(&less, huge, &wired("a")),
        Err(Reason::Fault(Fault::NotOfType { pin: "a".into() }))
    );
    let between = it("between", "duration", r#"{"low": 1, "high": 5}"#);
    assert_eq!(
        decide(&between, huge, &wired("value")),
        Err(Reason::Fault(Fault::NotOfType { pin: "value".into() }))
    );
}

#[test]
fn between_holds_its_ends_swaps_numbers_and_goes_past_midnight_for_times() {
    let between = |of: &str, value: &str, low: &str, high: &str| {
        yes(&it(
            "between",
            of,
            &format!(r#"{{"value": {value}, "low": {low}, "high": {high}}}"#),
        ))
    };
    assert!(between("number", "5", "5", "9"), "its low end");
    assert!(between("number", "9", "5", "9"), "its high end");
    assert!(!between("number", "4", "5", "9"));
    assert!(between("number", "5", "5", "5"), "equal bounds");
    assert!(between("number", "7", "9", "5"), "bounds swapped");
    assert!(
        between("number", "9", "9", "5") && between("number", "5", "9", "5"),
        "swapped, both ends"
    );
    assert!(!between("number", "10", "9", "5"));
    assert!(
        between("time", r#""02:00""#, r#""23:00""#, r#""08:00""#),
        "past midnight"
    );
    assert!(!between("time", r#""12:00""#, r#""23:00""#, r#""08:00""#));
    let ends = [r#""23:00""#, r#""08:00""#];
    assert!(
        ends.iter().all(|end| between("time", end, ends[0], ends[1])),
        "past midnight, both ends"
    );
    assert!(between("time", r#""12:00""#, r#""08:00""#, r#""23:00""#));
}

/// The life, which no message has said yet: reading it blocks the character.
const UNSEEN: &str =
    r#", {"id": "life", "kind": "life_percent"}, {"id": "ge", "kind": "greater_or_equal", "inputs": {"b": 50}}"#;
const UNSEEN_WIRE: &str = r#", {"from": ["life", "value"], "to": ["ge", "a"]}"#;

#[test]
fn and_and_or_stop_at_the_first_input_that_decides() {
    let logic = |kind: &str, first: bool| {
        let node = format!(r#"{{"id": "it", "kind": "{kind}", "inputs": {{"in_1": {first}}}}}"#);
        decide(
            &node,
            UNSEEN,
            &format!(r#"{UNSEEN_WIRE}, {{"from": ["ge", "value"], "to": ["it", "in_2"]}}"#),
        )
    };
    assert_eq!(logic("and", false), Ok("no".into()), "false AND anything");
    assert_eq!(logic("or", true), Ok("yes".into()), "true OR anything");
    let unseen = Err(Reason::Fault(Fault::NotSeen { what: "life".into() }));
    assert_eq!(logic("and", true), unseen, "its second read, as it must");
    let three = r#"{"id": "it", "kind": "and", "options": {"count": 3}, "inputs": {"in_1": true, "in_2": true, "in_3": false}}"#;
    assert_eq!(decide(three, "", ""), Ok("no".into()));
}

#[test]
fn not_and_choose_reading_only_the_side_it_takes() {
    assert!(!yes(r#"{"id": "it", "kind": "not", "inputs": {"value": true}}"#));
    let choose = |condition: bool| {
        let node = format!(
            r#"{{"id": "it", "kind": "choose", "options": {{"type": "bool"}}, "inputs": {{"condition": {condition}, "if_true": true}}}}"#
        );
        decide(
            &node,
            UNSEEN,
            &format!(r#"{UNSEEN_WIRE}, {{"from": ["ge", "value"], "to": ["it", "if_false"]}}"#),
        )
    };
    assert_eq!(choose(true), Ok("yes".into()), "its false side, unseen, never read");
    assert!(choose(false).is_err(), "its false side read");
}

/// The value `kind` gives of `inputs`, read by an `=` of its type against `expected`: whether it is that.
fn gives(kind: &str, inputs: &str, expected: i64) -> Result<bool, Reason> {
    let of = if kind == "seconds" || kind == "minutes" {
        "duration"
    } else {
        "number"
    };
    let node = it("equal", of, &format!(r#"{{"b": {expected}}}"#));
    let more = format!(r#", {{"id": "x", "kind": "{kind}", "inputs": {inputs}}}"#);
    decide(&node, &more, r#", {"from": ["x", "value"], "to": ["it", "a"]}"#).map(|said| said == "yes")
}

#[test]
fn the_arithmetic_of_whole_numbers_rounds_down_and_keeps_a_remainder_positive() {
    for (kind, inputs, expected) in [
        ("add", r#"{"a": 2, "b": 3}"#, 5),
        ("subtract", r#"{"a": 2, "b": 3}"#, -1),
        ("multiply", r#"{"a": -2, "b": 3}"#, -6),
        ("divide", r#"{"a": 7, "b": 2}"#, 3),
        ("divide", r#"{"a": -7, "b": 2}"#, -4),
        ("divide", r#"{"a": 7, "b": -2}"#, -4),
        ("divide", r#"{"a": -8, "b": 2}"#, -4),
        ("divide", r#"{"a": -7, "b": -2}"#, 3),
        ("remainder", r#"{"a": 7, "b": 3}"#, 1),
        ("remainder", r#"{"a": -7, "b": 3}"#, 2),
        ("remainder", r#"{"a": 7, "b": -3}"#, 1),
        ("min", r#"{"a": 2, "b": 3}"#, 2),
        ("max", r#"{"a": 2, "b": 3}"#, 3),
        ("clamp", r#"{"value": 12, "low": 1, "high": 10}"#, 10),
        ("clamp", r#"{"value": 5, "low": 10, "high": 1}"#, 5),
        ("clamp", r#"{"value": 7, "low": 3, "high": 3}"#, 3),
        ("clamp", r#"{"value": 5, "low": 1, "high": 10}"#, 5),
        ("percent_of", r#"{"percent": 50, "of": 7}"#, 3),
        ("percent_of", r#"{"percent": 25, "of": 3}"#, 0),
        ("percent_of", r#"{"percent": 150, "of": 10}"#, 15),
        ("percent_of", r#"{"percent": 50, "of": -7}"#, -4),
        ("seconds", r#"{"seconds": 45}"#, 45),
        ("minutes", r#"{"minutes": 2}"#, 120),
    ] {
        assert_eq!(gives(kind, inputs, expected), Ok(true), "{kind} {inputs}");
    }
}

#[test]
fn a_division_by_0_and_a_result_too_large_block_on_the_node() {
    // A 0 that comes by a wire, which the editor cannot see: 1 − 1.
    let more = r#", {"id": "x", "kind": "divide", "inputs": {"a": 5}}, {"id": "zero", "kind": "subtract", "inputs": {"a": 1, "b": 1}}"#;
    let wires = r#", {"from": ["x", "value"], "to": ["it", "a"]}, {"from": ["zero", "value"], "to": ["x", "b"]}"#;
    let node = it("equal", "number", r#"{"b": 0}"#);
    assert_eq!(
        decide(&node, more, wires),
        Err(Reason::Fault(Fault::DividedByZero { pin: "b".into() }))
    );
    let wired_zero = |kind: &str| {
        let more = format!(
            r#", {{"id": "x", "kind": "{kind}", "inputs": {{"a": 5}}}}, {{"id": "zero", "kind": "subtract", "inputs": {{"a": 1, "b": 1}}}}"#
        );
        decide(&node, &more, wires)
    };
    assert_eq!(
        wired_zero("remainder"),
        Err(Reason::Fault(Fault::DividedByZero { pin: "b".into() }))
    );
    let huge = format!(r#"{{"a": {}, "b": 2}}"#, i64::MAX);
    assert_eq!(gives("multiply", &huge, 0), Err(Reason::Fault(Fault::TooLarge)));
    assert_eq!(gives("add", &huge, 0), Err(Reason::Fault(Fault::TooLarge)));
    let below = format!(r#"{{"a": {}, "b": 1}}"#, i64::MIN);
    assert_eq!(gives("subtract", &below, 0), Err(Reason::Fault(Fault::TooLarge)));
    let lowest = format!(r#"{{"a": {}, "b": -1}}"#, i64::MIN);
    assert_eq!(
        gives("divide", &lowest, 0),
        Err(Reason::Fault(Fault::TooLarge)),
        "its quotient does not fit"
    );
    assert_eq!(gives("remainder", &lowest, 0), Ok(true), "its remainder, 0, fits");
    let wide = format!(r#"{{"percent": 50, "of": {}}}"#, i64::MAX);
    assert_eq!(
        gives("percent_of", &wide, i64::MAX / 2),
        Ok(true),
        "a wide product, a result that fits"
    );
    let past = format!(r#"{{"percent": 300, "of": {}}}"#, i64::MAX);
    assert_eq!(gives("percent_of", &past, 0), Err(Reason::Fault(Fault::TooLarge)));
    assert_eq!(
        gives("seconds", r#"{"seconds": -5}"#, 0),
        Err(Reason::Fault(Fault::NotOfType { pin: "seconds".into() }))
    );
}
