//! A blueprint's file of an older schema brought to this version's (ADR 0015): read as it was written, then migrated in
//! memory, and written as the newest at its next change, so that a file only read stays as it is. Schema 2 is v2's
//! (ADR 0016): "at least" becomes `≥` of two numbers, and Wait's minutes a duration.

use serde_json::Value;

use super::blueprint::{Blueprint, Does, End, Graph, Id, Node, Options, SCHEMA, Wire};
use super::variable::ValueType;

/// `blueprint` read from a file of its `schema`, brought to [`SCHEMA`].
pub(super) fn migrate(blueprint: &mut Blueprint) {
    if blueprint.schema == 1 {
        for graph in &mut blueprint.graphs {
            let waits = from_1(graph);
            // A minutes node sits left of the Wait it feeds, where the Wait is placed.
            if let Some(places) = blueprint.layout.get_mut(&graph.id) {
                for (wait, minutes) in waits {
                    if let Some([x, y]) = places.get(&wait).copied() {
                        places.insert(minutes, [x.saturating_sub(200), y]);
                    }
                }
            }
        }
    }
    blueprint.schema = SCHEMA;
}

/// `graph` from schema 1 to 2: each "at least" a `≥` of numbers, each Wait's minutes its duration in seconds, typed
/// there ×60 or, wired, through a node of minutes. The Waits fed so, each with its minutes node.
fn from_1(graph: &mut Graph) -> Vec<(Id, Id)> {
    let mut fed = Vec::new();
    for at in 0..graph.nodes.len() {
        let node = &mut graph.nodes[at];
        let Does::Kind(kind) = &node.does else { continue };
        match kind.0.as_str() {
            "at_least" => {
                node.does = Does::Kind(Id("greater_or_equal".to_owned()));
                node.options.value_type = Some(ValueType::Number);
            }
            "wait" => {
                let (minutes, duration) = (Id("minutes".to_owned()), Id("duration".to_owned()));
                if let Some(typed) = node.inputs.remove(&minutes) {
                    // A value no whole number of seconds holds stays as it was, its problem said.
                    let seconds = typed
                        .as_u64()
                        .and_then(|it| it.checked_mul(60))
                        .map_or(typed, Value::from);
                    node.inputs.insert(duration.clone(), seconds);
                }
                let wait = node.id.clone();
                let wired = graph
                    .wires
                    .iter()
                    .position(|it| it.to == End(wait.clone(), minutes.clone()));
                if let Some(wire) = wired {
                    let id = free(graph, &format!("{}_minutes", wait.0));
                    graph.wires[wire].to = End(id.clone(), minutes);
                    graph.wires.push(Wire {
                        from: End(id.clone(), Id("value".to_owned())),
                        to: End(wait.clone(), duration),
                    });
                    fed.push((wait, id));
                }
            }
            _ => {}
        }
    }
    for (_, id) in &fed {
        graph.nodes.push(Node {
            id: id.clone(),
            does: Does::Kind(Id("minutes".to_owned())),
            inputs: std::collections::BTreeMap::new(),
            event: None,
            options: Options::default(),
        });
    }
    fed
}

/// `base`, or `base_2`…, an id no node of `graph` holds nor a wire names, within the ids' length.
fn free(graph: &Graph, base: &str) -> Id {
    let base: String = base.chars().take(Id::LIMIT - 8).collect();
    let taken = |id: &str| {
        graph.nodes.iter().any(|node| node.id.0 == id)
            || graph.wires.iter().any(|wire| wire.from.0.0 == id || wire.to.0.0 == id)
    };
    let free = std::iter::once(base.clone())
        .chain((2..u32::MAX).map(|number| format!("{base}_{number}")))
        .find(|id| !taken(id));
    Id(free.unwrap_or(base))
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use crate::bot::blueprint::{Does, End, Id, Wire};
    use crate::bot::json::{read, write};
    use crate::bot::problems::problems;

    /// A schema 1 blueprint: at least 50 % of life, a Wait of 2 minutes typed, another fed its minutes by a variable.
    const SCHEMA_1: &str = r#"{"schema": 1, "id": "old", "name": "Old", "version": 1,
      "variables": [{"id": "pause", "name": "Pause", "type": "number", "default": 3}],
      "graphs": [{"id": "events", "name": "Events",
        "nodes": [{"id": "start", "kind": "on_start"}, {"id": "life", "kind": "life_percent"},
                  {"id": "half", "kind": "at_least", "inputs": {"b": 50}}, {"id": "if", "kind": "if"},
                  {"id": "wait", "kind": "wait", "inputs": {"minutes": 2}}, {"id": "pause", "get": "pause"},
                  {"id": "rest", "kind": "wait"}],
        "wires": [{"from": ["start", "then"], "to": ["if", "run"]}, {"from": ["life", "value"], "to": ["half", "a"]},
                  {"from": ["half", "value"], "to": ["if", "condition"]}, {"from": ["if", "true"], "to": ["wait", "run"]},
                  {"from": ["wait", "then"], "to": ["rest", "run"]}, {"from": ["pause", "value"], "to": ["rest", "minutes"]}]}],
      "layout": {"events": {"rest": [600, 100]}}}"#;

    #[test]
    fn a_schema_1_file_reads_as_2_its_at_least_a_comparison_and_its_minutes_a_duration_without_problems() {
        let blueprint = read(SCHEMA_1).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(blueprint.schema, 2);
        assert_eq!(problems(&blueprint), []);
        let events = &blueprint.graphs[0];
        let node = |id: &str| {
            events
                .nodes
                .iter()
                .find(|it| it.id.0 == id)
                .unwrap_or_else(|| panic!("{id}"))
        };
        assert_eq!(node("half").does, Does::Kind(crate::bot::Id("greater_or_equal".into())));
        assert_eq!(node("half").options.value_type, Some(crate::bot::ValueType::Number));
        assert_eq!(node("wait").inputs.values().collect::<Vec<_>>(), [&json!(120)]);
        assert_eq!(node("rest_minutes").does, Does::Kind(crate::bot::Id("minutes".into())));
        let end = |node: &str, pin: &str| End(Id(node.into()), Id(pin.into()));
        let wired = |from, to| events.wires.contains(&Wire { from, to });
        assert!(wired(end("pause", "value"), end("rest_minutes", "minutes")));
        assert!(wired(end("rest_minutes", "value"), end("rest", "duration")));
        let place = blueprint
            .layout
            .get(&Id("events".into()))
            .and_then(|it| it.get(&Id("rest_minutes".into())));
        assert_eq!(place, Some(&[400, 100]), "left of the Wait it feeds");
        let text = write(&blueprint);
        assert!(text.starts_with("{\n  \"schema\": 2,"), "{text}");
        assert_eq!(
            read(&text).ok().as_ref(),
            Some(&blueprint),
            "written as 2, read back the same"
        );
    }

    #[test]
    fn a_minutes_value_no_duration_holds_stays_and_says_its_problem() {
        let text = SCHEMA_1.replace(r#""inputs": {"minutes": 2}"#, r#""inputs": {"minutes": -2}"#);
        let blueprint = read(&text).unwrap_or_else(|error| panic!("{error}"));
        let found = problems(&blueprint);
        assert!(
            found
                .iter()
                .any(|it| matches!(it, crate::bot::Problem::ValueNotOfType { pin, .. } if pin.0 == "duration")),
            "{found:?}"
        );
    }

    #[test]
    fn a_minutes_node_takes_an_id_no_node_holds() {
        let text = SCHEMA_1.replace(
            r#"{"id": "pause", "get": "pause"}"#,
            r#"{"id": "pause", "get": "pause"}, {"id": "rest_minutes", "kind": "finish"}"#,
        );
        let blueprint = read(&text).unwrap_or_else(|error| panic!("{error}"));
        assert!(blueprint.graphs[0].nodes.iter().any(|it| it.id.0 == "rest_minutes_2"));
        assert_eq!(problems(&blueprint), []);
    }
}
