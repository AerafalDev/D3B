//! The catalogue of nodes (`docs/bot.md`, "In the app"): every kind of node a blueprint may hold, its family, its pins
//! and, for an event, how it runs. Rust holds ids only (the owner, 2026-11-13): the interface's translations hold a
//! kind's sentence and description under `kinds.<kind>` of the nodes' words (`locales/nodes.<language>.json`), its
//! pins' labels under `pins.<pin>`, by pin id across every kind, and the interface its icon. The bot's actions are listed before the step that makes them run,
//! marked with it, so that D3B's blueprints ship whole (the owner, 2026-11-13).

use super::variable::{Shape, Type, ValueType};

/// A node's family, which colours its icon.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Family {
    /// What starts a flow; only in an event graph.
    Event,
    /// What the character does in the game; it ends Done or Failed.
    Action,
    /// Where the flow goes.
    Flow,
    /// A value read when the node that uses it runs.
    Query,
    /// A function's entry and returns; only in a function.
    Function,
}

/// Which way a pin goes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(crate) enum Side {
    In,
    Out,
}

/// What a pin carries: the flow (a triangle), a value of a type (a circle, a square for a list), or a wildcard of a
/// shape, which takes the type its node notes (ADR 0016; `Options`'s `type`) and carries nothing until it does.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Carries {
    Exec,
    Value(Type),
    /// Any value, of any type: a Text's hole. It takes no type and gives none.
    Any,
    Wildcard(Shape),
}

impl Carries {
    /// One value of `of`.
    pub(crate) const fn one(of: ValueType) -> Self {
        Self::Value(Type::new(of, Shape::ONE))
    }

    /// Whether a wire may carry this output into `input`: the flow into the flow, a value into a pin of its type and
    /// shape, a percentage into a number, so a goal compares percentages without a conversion (the owner,
    /// 2026-11-13), and a list of items into the items actions take (ADR 0016). A wildcard carries nothing yet.
    pub(crate) fn feeds(self, input: Self) -> bool {
        match (self, input) {
            (Self::Exec, Self::Exec) | (Self::Value(_), Self::Any) => true,
            (Self::Value(from), Self::Value(to)) => {
                let items = from == Type::new(ValueType::Item, Shape::new(true, false))
                    && to == Type::new(ValueType::Items, Shape::ONE);
                let percentage = from.of == ValueType::Percentage && to.of == ValueType::Number;
                items || (from.shape == to.shape && (from.of == to.of || percentage))
            }
            _ => false,
        }
    }

    /// The type a wire between this output and `input` gives the wildcard at one of its ends: that of the other end,
    /// of the same shape. `None` when neither end is a wildcard, both are, or the shapes differ.
    pub(crate) fn types(self, input: Self) -> Option<ValueType> {
        match (self, input) {
            (Self::Wildcard(shape), Self::Value(typed)) | (Self::Value(typed), Self::Wildcard(shape)) => {
                (typed.shape == shape).then_some(typed.of)
            }
            _ => None,
        }
    }

    /// Whether it carries a value, or will once its wildcard is typed.
    pub(crate) fn is_value(self) -> bool {
        self != Self::Exec
    }
}

/// A pin of a kind of node.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Pin {
    pub(crate) id: &'static str,
    pub(crate) side: Side,
    pub(crate) carries: Carries,
    /// The value, as JSON, that an input no wire feeds and no value typed holds.
    pub(crate) default: Option<&'static str>,
}

const fn exec(id: &'static str, side: Side) -> Pin {
    Pin {
        id,
        side,
        carries: Carries::Exec,
        default: None,
    }
}

const fn input(id: &'static str, value_type: ValueType, default: Option<&'static str>) -> Pin {
    Pin {
        id,
        side: Side::In,
        carries: Carries::one(value_type),
        default,
    }
}

const fn wildcard(id: &'static str, side: Side, list: bool) -> Pin {
    Pin {
        id,
        side,
        carries: Carries::Wildcard(Shape::new(list, false)),
        default: None,
    }
}

const fn output(id: &'static str, value_type: ValueType) -> Pin {
    Pin {
        id,
        side: Side::Out,
        carries: Carries::one(value_type),
        default: None,
    }
}

/// The flow in.
pub(crate) const RUN: Pin = exec("run", Side::In);
/// The flow out of an event or a flow node.
pub(crate) const THEN: Pin = exec("then", Side::Out);
/// The flow out of an action that did what it says.
pub(crate) const DONE: Pin = exec("done", Side::Out);
/// The flow out of an action that could not; left unlinked, it blocks the character with its reason.
pub(crate) const FAILED: Pin = exec("failed", Side::Out);
/// Why an action failed.
pub(crate) const REASON: Pin = output("reason", ValueType::Text);

/// The pins of a function's built-in nodes and calls, which its inputs and outputs cannot take.
pub(crate) const RESERVED: [&str; 5] = ["run", "then", "done", "failed", "reason"];

/// How an event runs (`docs/bot.md`, "How a blueprint runs").
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct EventRole {
    /// Whether it takes a priority and a mode.
    pub(crate) settings: bool,
    /// Whether a blueprint holds it once at most.
    pub(crate) once: bool,
    /// What happens when the game forces it and the blueprint does not handle it.
    pub(crate) forced: Option<Fallback>,
}

/// What a forced event does when the blueprint leaves it out.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Fallback {
    /// The default fight AI fights (step 6), then the flow resumes.
    DefaultFight,
    /// The run stops, blocked with the reason.
    Block,
}

/// An option a kind takes (`Options`), and the pins it makes.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Takes {
    /// `count`: the pins `<pin>_1` to `<pin>_<count>`, as `pin` is, `least` of them when the node gives no count.
    Count { pin: Pin, least: u8 },
    /// `cases`: a flow output `case_<n>` per case, each a value of the node's type.
    Cases,
    /// `text`: an input of any value per hole of the template, named by it.
    Text,
    /// `expression`: a formula, its pins none (v2.8).
    Expression,
    /// `variable`: the variable it changes, whose type its wildcards take, rather than `type`.
    Variable,
    /// `event`: the event of mine it is, its inputs its outputs.
    Event,
    /// `exit`: the function's exit it returns by.
    Exit,
}

/// A kind of node.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Kind {
    pub(crate) id: &'static str,
    pub(crate) family: Family,
    pub(crate) pins: &'static [Pin],
    pub(crate) event: Option<EventRole>,
    /// The roadmap step from which the bot does it in the game; the picker marks it until then.
    pub(crate) from_step: Option<u8>,
    /// The options it takes beyond its wildcards' type.
    pub(crate) takes: &'static [Takes],
    /// The types its wildcards may take, the first while its node notes none (the owner, 2026-11-17: `≥` reads numbers
    /// as "at least" did until the editor types it, (v2.2)); empty for any type and none by default.
    pub(crate) types: &'static [ValueType],
}

const fn event(id: &'static str, pins: &'static [Pin], role: EventRole) -> Kind {
    Kind {
        id,
        family: Family::Event,
        pins,
        event: Some(role),
        from_step: None,
        takes: &[],
        types: &[],
    }
}

const fn of(id: &'static str, family: Family, pins: &'static [Pin], from_step: Option<u8>) -> Kind {
    Kind {
        id,
        family,
        pins,
        event: None,
        from_step,
        takes: &[],
        types: &[],
    }
}

const MAIN: EventRole = EventRole {
    settings: false,
    once: true,
    forced: None,
};
const THRESHOLD: EventRole = EventRole {
    settings: true,
    once: false,
    forced: None,
};
const FIGHT: EventRole = EventRole {
    settings: true,
    once: true,
    forced: Some(Fallback::DefaultFight),
};
const BLOCKS: EventRole = EventRole {
    settings: false,
    once: true,
    forced: Some(Fallback::Block),
};
/// An event of mine: once per event it names, which `wiring::once_twice` counts.
const MINE: EventRole = EventRole {
    settings: true,
    once: false,
    forced: None,
};

use Family::{Action, Flow, Function, Query};
use ValueType::{Bank, Bool, Duration, Items, Job, Number, Percentage, Text, Zone};

/// Every kind, the owner's v1 list of 2026-11-13: what D3B's blueprints and the six scenarios need, the rest later.
#[rustfmt::skip] // One kind per line reads as a table.
pub(crate) const KINDS: &[Kind] = &[
    event("on_start", &[THEN], MAIN),
    event("life_below", &[THEN, input("threshold", Percentage, Some("40")), input("rearm", Percentage, Some("60"))], THRESHOLD),
    event("pods_above", &[THEN, input("threshold", Percentage, Some("90")), input("rearm", Percentage, Some("70"))], THRESHOLD),
    event("fight_starts", &[THEN], FIGHT),
    event("died", &[THEN], BLOCKS),
    event("action_failed", &[THEN, REASON], BLOCKS),
    of("go_to_zone", Action, &[RUN, DONE, FAILED, REASON, input("zone", Zone, None)], Some(4)),
    of("harvest_until", Action, &[RUN, DONE, FAILED, REASON, input("zone", Zone, None), input("what", Items, Some("\"all\"")), input("pods", Percentage, Some("90")), output("count", Number)], Some(5)),
    of("go_to_bank", Action, &[RUN, DONE, FAILED, REASON, input("bank", Bank, Some("\"nearest\""))], Some(5)),
    of("deposit_all_but", Action, &[RUN, DONE, FAILED, REASON, input("keep", Items, Some("\"none\""))], Some(5)),
    of("hunt_until", Action, &[RUN, DONE, FAILED, REASON, input("zone", Zone, None), input("pods", Percentage, Some("90"))], Some(6)),
    of("notify", Action, &[RUN, DONE, input("text", Text, None)], None),
    of("if", Flow, &[RUN, input("condition", Bool, None), exec("true", Side::Out), exec("false", Side::Out)], None),
    of("wait", Flow, &[RUN, THEN, input("duration", Duration, Some("60"))], None),
    // Sitting no longer gives life back faster: life comes back alone, out of a fight (the owner, 2026-11-14).
    of("wait_for_life", Flow, &[RUN, THEN, input("life", Percentage, Some("90"))], None),
    of("finish", Flow, &[RUN], None),
    // v2's flow (`docs/bot.md`, "The flow's kinds (v2.3)"): Sequence runs its outputs in turn, its last the rest of the
    // flow; Switch follows the case its value is, or Otherwise.
    Kind { takes: &[Takes::Count { pin: exec("then", Side::Out), least: 2 }], ..of("sequence", Flow, &[RUN], None) },
    Kind { takes: &[Takes::Cases], ..of("switch", Flow, &[RUN, wildcard("value", Side::In, false), exec("otherwise", Side::Out)], None) },
    // The loops (the owner, 2026-11-17): each its body, run again as its flow ends, then Completed; indexes from 1, a
    // list read once as the flow enters; Leave the loop ends the nearest one's body and goes on out of its Completed.
    of("repeat", Flow, &[RUN, input("times", Number, Some("3")), exec("body", Side::Out), output("index", Number), exec("completed", Side::Out)], None),
    of("while", Flow, &[RUN, input("condition", Bool, None), exec("body", Side::Out), exec("completed", Side::Out)], None),
    of("for_each", Flow, &[RUN, wildcard("list", Side::In, true), exec("body", Side::Out), wildcard("element", Side::Out, false), output("index", Number), exec("completed", Side::Out)], None),
    of("leave_loop", Flow, &[RUN], None),
    // The kinds that remember, each its memory for the whole run (the owner, 2026-11-17): Do N times, 1 by default for
    // Do once, re-armed by its second flow input; Alternate, first then second; Pick the least recently picked.
    of("do_n_times", Flow, &[RUN, exec("reset", Side::In), input("times", Number, Some("1")), THEN], None),
    of("alternate", Flow, &[RUN, exec("first", Side::Out), exec("second", Side::Out)], None),
    of("pick", Flow, &[RUN, wildcard("list", Side::In, true), THEN, wildcard("element", Side::Out, false)], None),
    // Waits and stops (the owner, 2026-11-17): Wait until a condition, at most a duration (0 for no limit); Block with a
    // reason; Pause and notify, which pauses the run past itself, so that resuming (step 4) goes on after it.
    of("wait_until", Flow, &[RUN, input("condition", Bool, None), input("longest", Duration, Some("0")), THEN, exec("too_long", Side::Out)], None),
    of("block", Flow, &[RUN, input("reason", Text, None)], None),
    of("pause_notify", Flow, &[RUN, input("text", Text, None), THEN], None),
    // Retry (the owner, 2026-11-17): a node whose body is tried up to N times, the first included; an unlinked Failed
    // inside it starts the next try, and after the last one it goes out of Failed with the reason.
    of("retry", Flow, &[RUN, input("times", Number, Some("3")), exec("try", Side::Out), output("index", Number), THEN, FAILED, REASON], None),
    of("life_percent", Query, &[output("value", Percentage)], None),
    of("pods_percent", Query, &[output("value", Percentage)], None),
    // The items a profession harvests, from the data's skills (3.7.13.17, `artifacts/studies/items.md`, verified):
    // `job-harvest` reads its setting through it (the owner, 2026-11-14).
    of("job_resources", Query, &[input("job", Job, None), output("value", Items)], None),
    of("level", Query, &[output("value", Number)], None),
    // v1's "at least", now any two values compared in order (ADR 0016): numbers, percentages, durations, times.
    Kind { types: &[Number, Percentage, Duration, ValueType::Time], ..of("greater_or_equal", Query, &[wildcard("a", Side::In, false), wildcard("b", Side::In, false), output("value", Bool)], None) },
    of("minutes", Query, &[input("minutes", Number, Some("1")), output("value", Duration)], None),
    of("add", Query, &[input("a", Number, None), input("b", Number, None), output("value", Number)], None),
    of("entry", Function, &[THEN], None),
    of("return_done", Function, &[RUN], None),
    of("return_failed", Function, &[RUN, input("reason", Text, Some("\"\""))], None),
    // v2's (ADR 0016): an event of mine, a return by a named exit, and a pure function's result, its outputs its inputs.
    Kind { takes: &[Takes::Event], ..event("my_event", &[THEN], MINE) },
    Kind { takes: &[Takes::Exit], ..of("return_exit", Function, &[RUN], None) },
    of("result", Function, &[], None),
];

/// The kind of this id, if this version knows it.
pub(crate) fn kind(id: &str) -> Option<&'static Kind> {
    let kinds = KINDS.iter();
    #[cfg(test)]
    let kinds = kinds.chain(tests::WILDCARDS);
    kinds.into_iter().find(|kind| kind.id == id)
}

#[cfg(test)]
pub(crate) mod tests {
    use std::collections::HashSet;

    use super::*;
    use crate::bot::blueprint::Id;

    const fn taking(kind: Kind, takes: &'static [Takes]) -> Kind {
        Kind { takes, ..kind }
    }

    /// Kinds for the tests, until v2's values bring theirs: `=` of two values of one type, a list of its numbered
    /// inputs, Text, Add to a variable and an Expression.
    #[rustfmt::skip]
    pub(crate) const WILDCARDS: &[Kind] = &[
        of("test_equal", Query, &[wildcard("a", Side::In, false), wildcard("b", Side::In, false), output("value", Bool)], None),
        taking(of("test_list", Query, &[output("value", Number)], None), &[Takes::Count { pin: input("in", Number, None), least: 1 }]),
        taking(of("test_text", Query, &[output("value", Text)], None), &[Takes::Text]),
        taking(of("test_add_to", Flow, &[RUN, THEN, wildcard("amount", Side::In, false)], None), &[Takes::Variable]),
        taking(of("test_expression", Query, &[wildcard("value", Side::Out, false)], None), &[Takes::Expression]),
    ];

    #[test]
    fn every_kind_and_pin_has_a_well_formed_id_unique_where_it_is() {
        let mut kinds = HashSet::new();
        for kind in KINDS {
            assert!(
                Id(kind.id.to_owned()).is_well_formed() && kinds.insert(kind.id),
                "{}",
                kind.id
            );
            let mut pins = HashSet::new();
            for pin in kind.pins {
                assert!(
                    Id(pin.id.to_owned()).is_well_formed() && pins.insert((pin.id, pin.side)),
                    "{}",
                    pin.id
                );
            }
        }
    }

    #[test]
    fn every_default_is_held_by_its_pin_and_only_inputs_have_one() {
        for pin in KINDS.iter().flat_map(|kind| kind.pins) {
            let Some(default) = pin.default else { continue };
            let value = serde_json::from_str(default).unwrap_or_else(|error| panic!("{}: {error}", pin.id));
            assert!(
                matches!(pin.carries, Carries::Value(typed) if typed.holds(&value)),
                "{}",
                pin.id
            );
            assert_eq!(pin.side, Side::In, "{}", pin.id);
        }
    }

    #[test]
    fn only_events_have_a_role_and_every_action_ends_done() {
        for kind in KINDS {
            assert_eq!(kind.event.is_some(), kind.family == Family::Event, "{}", kind.id);
            if kind.family == Family::Action {
                assert!(kind.pins.contains(&RUN) && kind.pins.contains(&DONE), "{}", kind.id);
            }
        }
    }

    #[test]
    fn the_forced_events_have_their_defaults() {
        let forced = |id| kind(id).and_then(|kind| kind.event).and_then(|role| role.forced);
        assert_eq!(forced("fight_starts"), Some(Fallback::DefaultFight));
        assert_eq!(forced("died"), Some(Fallback::Block));
        assert_eq!(forced("action_failed"), Some(Fallback::Block));
    }

    #[test]
    fn the_game_actions_say_the_step_that_makes_them_run() {
        let step = |id| kind(id).and_then(|kind| kind.from_step);
        assert_eq!(
            (step("go_to_zone"), step("harvest_until"), step("hunt_until")),
            (Some(4), Some(5), Some(6))
        );
        assert_eq!(step("notify"), None);
        // Waiting for life is no game action: life comes back alone (the owner, 2026-11-14).
        assert_eq!((step("wait_for_life"), kind("sit_until")), (None, None));
        assert_eq!(kind("wait_for_life").map(|kind| kind.family), Some(Family::Flow));
    }

    #[test]
    fn a_wire_carries_a_list_into_a_list_and_a_list_of_items_into_items() {
        let (one, list, ranges) = (Shape::ONE, Shape::new(true, false), Shape::new(false, true));
        let of = |value_type, shape| Carries::Value(Type::new(value_type, shape));
        assert!(of(Zone, list).feeds(of(Zone, list)));
        assert!(of(Percentage, list).feeds(of(Number, list)));
        assert!(!of(Zone, list).feeds(of(Zone, one)), "a list into one value");
        assert!(!of(Zone, one).feeds(of(Zone, list)), "one value into a list");
        assert!(!of(Zone, ranges).feeds(of(Zone, one)));
        assert!(
            of(ValueType::Item, list).feeds(of(Items, one)),
            "a list of items is items"
        );
        assert!(
            !of(Items, one).feeds(of(ValueType::Item, list)),
            "not the other way: all is no list"
        );
    }

    #[test]
    fn a_wildcard_carries_nothing_and_takes_the_type_of_a_wire_of_its_shape() {
        let (one, list) = (Shape::ONE, Shape::new(true, false));
        let of = |value_type, shape| Carries::Value(Type::new(value_type, shape));
        let (any, any_list) = (Carries::Wildcard(one), Carries::Wildcard(list));
        assert!(!any.feeds(any) && !of(Zone, one).feeds(any) && !any.feeds(of(Zone, one)));
        assert_eq!(of(Percentage, one).types(any), Some(Percentage));
        assert_eq!(any.types(of(Bank, one)), Some(Bank));
        assert_eq!(of(Zone, list).types(any_list), Some(Zone));
        assert_eq!(of(Zone, list).types(any), None, "a list into one value");
        let none = (
            any.types(any),
            Carries::Exec.types(any),
            of(Zone, one).types(of(Zone, one)),
        );
        assert_eq!(none, (None, None, None));
        assert!(any.is_value() && !Carries::Exec.is_value());
    }

    #[test]
    fn a_wire_carries_the_flow_into_the_flow_and_a_value_into_its_type() {
        let value = Carries::one;
        assert!(Carries::Exec.feeds(Carries::Exec));
        assert!(value(Zone).feeds(value(Zone)));
        assert!(value(Percentage).feeds(value(Number)), "a percentage feeds a number");
        assert!(!value(Number).feeds(value(Percentage)), "not the other way");
        assert!(!Carries::Exec.feeds(value(Bool)) && !value(Bool).feeds(Carries::Exec));
        assert!(!value(Number).feeds(value(Text)));
    }

    #[test]
    fn every_kind_reads_as_a_sentence_in_each_language_whose_holes_are_its_inputs() {
        let locales = [
            include_str!("../../../src/i18n/locales/nodes.en.json"),
            include_str!("../../../src/i18n/locales/nodes.fr.json"),
        ];
        let mut holes_by_language = Vec::new();
        for text in locales {
            let locale: serde_json::Value = serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
            let (kinds, labels) = (&locale["kinds"], &locale["pins"]);
            let listed = kinds
                .as_object()
                .map(|kinds| kinds.keys().cloned().collect::<HashSet<_>>());
            assert_eq!(
                listed,
                Some(KINDS.iter().map(|kind| kind.id.to_owned()).collect()),
                "no kind more or less"
            );
            let mut holes = Vec::new();
            for kind in KINDS {
                let read = |key: &str| kinds[kind.id][key].as_str().filter(|text| !text.is_empty());
                let (Some(sentence), Some(_)) = (read("sentence"), read("description")) else {
                    panic!("{}", kind.id)
                };
                for rest in sentence.split("{{").skip(1) {
                    let hole = rest
                        .split_once("}}")
                        .map_or_else(|| panic!("{}: unclosed", kind.id), |(hole, _)| hole);
                    let input = kind
                        .pins
                        .iter()
                        .any(|pin| pin.id == hole && pin.side == Side::In && pin.carries.is_value());
                    assert!(input, "{}: {hole}", kind.id);
                    holes.push((kind.id, hole.to_owned()));
                }
            }
            // A label for every pin, empty only for the flow's own: no more, no less.
            let pins: HashSet<&str> = KINDS.iter().flat_map(|kind| kind.pins).map(|pin| pin.id).collect();
            let labelled = labels
                .as_object()
                .map(|labels| labels.keys().map(String::as_str).collect::<HashSet<_>>());
            assert_eq!(labelled, Some(pins.clone()));
            for pin in pins {
                let empty = labels[pin].as_str().map(str::is_empty);
                assert_eq!(empty, Some(pin == "run" || pin == "then"), "{pin}");
            }
            holes.sort();
            holes_by_language.push(holes);
        }
        assert_eq!(
            holes_by_language[0], holes_by_language[1],
            "the same holes in English and French"
        );
    }
}
