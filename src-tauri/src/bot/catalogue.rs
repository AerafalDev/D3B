//! The catalogue of nodes (`docs/bot.md`, "In the app"): every kind of node a blueprint may hold, its family, its pins
//! and, for an event, how it runs. Rust holds ids only: a kind's sentence, description and pins' labels are the
//! interface's translations under `node.<kind>`, its icon the interface's (the owner, 2026-11-13). The bot's actions
//! are listed before the step that makes them run, marked with it, so that D3B's blueprints ship whole (the owner,
//! 2026-11-13).

use super::variable::ValueType;

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

/// What a pin carries: the flow (a triangle) or a value of a type (a circle).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Carries {
    Exec,
    Value(ValueType),
}

impl Carries {
    /// Whether a wire may carry this output into `input`: the flow into the flow, a value into a pin of its type, and
    /// a percentage into a number, so a goal compares percentages without a conversion (the owner, 2026-11-13).
    pub(crate) fn feeds(self, input: Self) -> bool {
        match (self, input) {
            (Self::Exec, Self::Exec) => true,
            (Self::Value(from), Self::Value(to)) => {
                from == to || (from == ValueType::Percentage && to == ValueType::Number)
            }
            _ => false,
        }
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
        carries: Carries::Value(value_type),
        default,
    }
}

const fn output(id: &'static str, value_type: ValueType) -> Pin {
    Pin {
        id,
        side: Side::Out,
        carries: Carries::Value(value_type),
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

/// A kind of node.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Kind {
    pub(crate) id: &'static str,
    pub(crate) family: Family,
    pub(crate) pins: &'static [Pin],
    pub(crate) event: Option<EventRole>,
    /// The roadmap step from which the bot does it in the game; the picker marks it until then.
    pub(crate) from_step: Option<u8>,
}

const fn event(id: &'static str, pins: &'static [Pin], role: EventRole) -> Kind {
    Kind {
        id,
        family: Family::Event,
        pins,
        event: Some(role),
        from_step: None,
    }
}

const fn of(id: &'static str, family: Family, pins: &'static [Pin], from_step: Option<u8>) -> Kind {
    Kind {
        id,
        family,
        pins,
        event: None,
        from_step,
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

use Family::{Action, Flow, Function, Query};
use ValueType::{Bank, Bool, Items, Number, Percentage, Text, Zone};

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
    of("sit_until", Action, &[RUN, DONE, FAILED, REASON, input("life", Percentage, Some("90"))], Some(4)),
    of("harvest_until", Action, &[RUN, DONE, FAILED, REASON, input("zone", Zone, None), input("what", Items, Some("\"all\"")), input("pods", Percentage, Some("90")), output("count", Number)], Some(5)),
    of("go_to_bank", Action, &[RUN, DONE, FAILED, REASON, input("bank", Bank, Some("\"nearest\""))], Some(5)),
    of("deposit_all_but", Action, &[RUN, DONE, FAILED, REASON, input("keep", Items, Some("\"none\""))], Some(5)),
    of("hunt_until", Action, &[RUN, DONE, FAILED, REASON, input("zone", Zone, None), input("pods", Percentage, Some("90"))], Some(6)),
    of("notify", Action, &[RUN, DONE, input("text", Text, None)], None),
    of("if", Flow, &[RUN, input("condition", Bool, None), exec("true", Side::Out), exec("false", Side::Out)], None),
    of("wait", Flow, &[RUN, THEN, input("minutes", Number, Some("1"))], None),
    of("finish", Flow, &[RUN], None),
    of("life_percent", Query, &[output("value", Percentage)], None),
    of("pods_percent", Query, &[output("value", Percentage)], None),
    of("level", Query, &[output("value", Number)], None),
    of("at_least", Query, &[input("a", Number, None), input("b", Number, None), output("value", Bool)], None),
    of("add", Query, &[input("a", Number, None), input("b", Number, None), output("value", Number)], None),
    of("entry", Function, &[THEN], None),
    of("return_done", Function, &[RUN], None),
    of("return_failed", Function, &[RUN, input("reason", Text, Some("\"\""))], None),
];

/// The kind of this id, if this version knows it.
pub(crate) fn kind(id: &str) -> Option<&'static Kind> {
    KINDS.iter().find(|kind| kind.id == id)
}

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use super::*;
    use crate::bot::blueprint::Id;

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
                matches!(pin.carries, Carries::Value(value_type) if value_type.holds(&value)),
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
    }

    #[test]
    fn a_wire_carries_the_flow_into_the_flow_and_a_value_into_its_type() {
        let value = Carries::Value;
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
            include_str!("../../../src/i18n/locales/en.json"),
            include_str!("../../../src/i18n/locales/fr.json"),
        ];
        for text in locales {
            let locale: serde_json::Value = serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
            let (kinds, pins) = (&locale["node"]["kinds"], &locale["node"]["pins"]);
            let listed = kinds
                .as_object()
                .map(|kinds| kinds.keys().cloned().collect::<HashSet<_>>());
            assert_eq!(
                listed,
                Some(KINDS.iter().map(|kind| kind.id.to_owned()).collect()),
                "no kind more or less"
            );
            for kind in KINDS {
                let read = |key: &str| kinds[kind.id][key].as_str().filter(|text| !text.is_empty());
                let (Some(sentence), Some(_)) = (read("sentence"), read("description")) else {
                    panic!("{}", kind.id)
                };
                for hole in sentence
                    .split("{{")
                    .skip(1)
                    .filter_map(|rest| rest.split_once("}}"))
                    .map(|(hole, _)| hole)
                {
                    let value = |pin: &&Pin| matches!(pin.carries, Carries::Value(_));
                    let input = kind
                        .pins
                        .iter()
                        .filter(value)
                        .any(|pin| pin.id == hole && pin.side == Side::In);
                    assert!(input, "{}: {hole}", kind.id);
                }
                for pin in kind.pins {
                    assert!(pins[pin.id].is_string(), "{}: {}", kind.id, pin.id);
                }
            }
        }
    }
}
