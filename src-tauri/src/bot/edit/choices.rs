//! What the editor's picker may add to a graph (`docs/bot.md`, "In the app"; the owner, 2026-11-16): the catalogue's
//! kinds that have their place there, then the blueprint's own variables, each read and written, and its functions
//! called. An event goes in an event graph, and not again when the blueprint holds it once at most; a function's entry
//! and returns go in a function, its entry once: what would only add a problem is not offered.

use std::collections::HashSet;

use super::super::blueprint::{Blueprint, Does, Graph, Id};
use super::super::catalogue::{Family, KINDS, Takes};

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
            Family::Function => in_function && !(kind.id == "entry" && has_entry),
            Family::Action | Family::Flow | Family::Query => true,
        })
        .map(|kind| Does::Kind(Id(kind.id.to_owned())));
    // An id given twice, or malformed, is its own problem: the first well-formed one is what its nodes mean, and a
    // node's id is read from it.
    let mut variables = HashSet::new();
    let variables = blueprint
        .variables
        .iter()
        .filter(move |it| it.id.is_well_formed() && variables.insert(&it.id))
        .flat_map(|it| [Does::Get(it.id.clone()), Does::Set(it.id.clone())]);
    let mut functions = HashSet::new();
    let functions = blueprint
        .graphs
        .iter()
        .filter(move |it| it.function.is_some() && it.id.is_well_formed() && functions.insert(&it.id))
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
    }
}
