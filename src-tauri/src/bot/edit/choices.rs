//! What the editor's picker may add to a graph (`docs/bot.md`, "In the app"; the owner, 2026-11-16): the catalogue's
//! kinds that have their place there, then the blueprint's own variables, each read and written, and its functions
//! called. An event goes in an event graph, and not again when the blueprint holds it once at most; a function's entry
//! and returns go in a function, its entry once: what would only add a problem is not offered.

use std::collections::HashSet;

use super::super::blueprint::{Blueprint, Does, Graph, Id};
use super::super::catalogue::{Carries, Family, KINDS, Takes};

/// What may be added to `graph`, in the catalogue's order, then the gets and sets, then the calls.
pub(crate) fn choices(blueprint: &Blueprint, graph: &Graph) -> Vec<Does> {
    let in_function = graph.function.is_some();
    let pure = graph.function.as_ref().is_some_and(|signature| signature.pure);
    // Held once at most across the blueprint's event graphs, as `wiring::once_twice` counts them.
    let held: HashSet<&str> = blueprint
        .graphs
        .iter()
        .filter(|it| it.function.is_none())
        .flat_map(|it| &it.nodes)
        .filter_map(|node| match &node.does {
            Does::Kind(kind) => Some(kind.0.as_str()),
            _ => None,
        })
        .collect();
    let has_entry = graph
        .nodes
        .iter()
        .any(|node| matches!(&node.does, Does::Kind(kind) if kind.0 == "entry"));
    let kinds = KINDS
        .iter()
        .filter(|kind| match kind.family {
            Family::Event if in_function => false,
            // An event of mine and a return by an exit name theirs: the editor adds them from My Blueprint ((15), (16)).
            _ if kind.takes.iter().any(|it| matches!(it, Takes::Event | Takes::Exit)) => false,
            Family::Event => !(kind.event.is_some_and(|role| role.once) && held.contains(kind.id)),
            Family::Function if kind.id == "result" => pure,
            // A pure function computes values only: nothing with a flow.
            _ if pure
                && kind
                    .pins
                    .iter()
                    .any(|pin| pin.carries == Carries::Exec && kind.id != "entry") =>
            {
                false
            }
            Family::Function => in_function && !(kind.id == "entry" && has_entry),
            Family::Action | Family::Flow | Family::Query | Family::Value => true,
        })
        .map(|kind| Does::Kind(Id(kind.id.to_owned())));
    // An id given twice, or malformed, is its own problem: the first well-formed one is what its nodes mean, and a
    // node's id is read from it.
    let mut variables = HashSet::new();
    let variables = blueprint
        .variables
        .iter()
        .chain(graph.function.iter().flat_map(|it| &it.locals))
        .filter(move |it| it.id.is_well_formed() && variables.insert(&it.id))
        .flat_map(move |it| {
            let set = (!pure).then(|| Does::Set(it.id.clone()));
            std::iter::once(Does::Get(it.id.clone())).chain(set)
        });
    let mut functions = HashSet::new();
    let functions = blueprint
        .graphs
        .iter()
        .filter(move |it| it.function.is_some() && it.id.is_well_formed() && functions.insert(&it.id))
        // A pure function calls pure ones only.
        .filter(move |it| !pure || it.function.as_ref().is_some_and(|signature| signature.pure))
        .map(|it| Does::Call(it.id.clone()));
    kinds.chain(variables).chain(functions).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::json::{read, tests::HARVEST};

    fn kind(id: &str) -> Does {
        Does::Kind(Id(id.into()))
    }

    #[test]
    fn an_event_graph_offers_the_events_not_held_yet_the_other_kinds_the_variables_and_the_functions() {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let offered = choices(&blueprint, &blueprint.graphs[0]);
        for it in ["life_below", "fight_starts", "notify", "if", "life_percent"] {
            assert!(offered.contains(&kind(it)), "{it}");
        }
        for it in ["on_start", "entry", "return_done", "return_failed"] {
            assert!(!offered.contains(&kind(it)), "{it}: held once, or a function's");
        }
        let zone = Id("zone".into());
        let mut malformed = blueprint.clone();
        malformed.variables[0].id = Id("Zone".into());
        let offered_malformed = choices(&malformed, &malformed.graphs[0]);
        assert!(
            !offered_malformed.contains(&Does::Get(Id("Zone".into()))),
            "a malformed id is its own problem"
        );
        assert!(offered.contains(&Does::Get(zone.clone())) && offered.contains(&Does::Set(zone)));
        assert_eq!(offered.last(), Some(&Does::Call(Id("empty_at_bank".into()))));
    }

    #[test]
    fn a_function_offers_its_entry_once_and_its_returns_but_no_event() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let offered = choices(&blueprint, &blueprint.graphs[1]);
        for it in ["entry", "return_done", "return_failed", "wait"] {
            assert!(offered.contains(&kind(it)), "{it}");
        }
        assert!(!offered.contains(&kind("life_below")));
        let entry = serde_json::from_str(r#"{"id": "in", "kind": "entry"}"#).unwrap_or_else(|error| panic!("{error}"));
        blueprint.graphs[1].nodes.push(entry);
        assert!(!choices(&blueprint, &blueprint.graphs[1]).contains(&kind("entry")));
        assert!(!offered.contains(&kind("result")), "a result is a pure function's");
    }

    #[test]
    fn a_pure_function_offers_no_flow_and_none_offers_what_names_an_event_or_an_exit() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let pure = r#"{"id": "half", "name": "Half", "function": {"pure": true,
          "locals": [{"id": "tries", "name": "Tries", "type": "number"}]}}"#;
        blueprint
            .graphs
            .push(serde_json::from_str(pure).unwrap_or_else(|error| panic!("{error}")));
        let offered = choices(&blueprint, &blueprint.graphs[2]);
        for it in ["entry", "result", "level", "add"] {
            assert!(offered.contains(&kind(it)), "{it}");
        }
        for it in ["notify", "wait", "return_done", "return_exit", "if"] {
            assert!(!offered.contains(&kind(it)), "{it}");
        }
        let tries = Id("tries".into());
        assert!(offered.contains(&Does::Get(tries.clone())) && !offered.contains(&Does::Set(tries)));
        assert!(
            offered.contains(&Does::Call(Id("half".into())))
                && !offered.contains(&Does::Call(Id("empty_at_bank".into())))
        );
        assert!(!offered.iter().any(|it| matches!(it, Does::Set(_))), "a Set has a flow");
        let events = choices(&blueprint, &blueprint.graphs[0]);
        assert!(!events.contains(&kind("my_event")) && !events.contains(&kind("result")));
    }
}
