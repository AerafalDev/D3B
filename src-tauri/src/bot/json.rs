//! A blueprint's JSON file (ADR 0015): read with its `schema` checked first, so that a file a newer D3B wrote is
//! refused untouched rather than half read, and written the same way every time, so that a diff shows only what
//! changed.

use super::blueprint::{Blueprint, SCHEMA};

/// A blueprint file that cannot be read; it is kept as it is and reported, never overwritten.
#[derive(Debug, thiserror::Error)]
pub(crate) enum ReadError {
    #[error("written by a newer D3B: schema {found}, and this version reads up to {SCHEMA}")]
    Newer { found: u64 },
    #[error("not a blueprint of schema {SCHEMA}: {0}")]
    NotABlueprint(#[from] serde_json::Error),
}

/// The blueprint `text` holds. The schema is read first, then the whole text again into the blueprint, so that a key
/// given twice is refused and an error says its line and column.
pub(crate) fn read(text: &str) -> Result<Blueprint, ReadError> {
    #[derive(serde::Deserialize)]
    struct Head {
        schema: Option<serde_json::Value>,
    }
    let head: Head = serde_json::from_str(text)?;
    match head.schema.as_ref().and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(SCHEMA) => Err(ReadError::Newer { found }),
        Some(found) if found == u64::from(SCHEMA) => Ok(serde_json::from_str(text)?),
        _ => Err(ReadError::NotABlueprint(serde::de::Error::custom(format!(
            "its schema must be {SCHEMA}"
        )))),
    }
}

/// The text of `blueprint`'s file: indented, its maps in the order of their keys, ending with a new line.
pub(crate) fn write(blueprint: &Blueprint) -> String {
    // A blueprint is only strings, numbers, maps with string keys and JSON values: nothing serde_json can refuse.
    let mut text = serde_json::to_string_pretty(blueprint).unwrap_or_else(|error| unreachable!("{error}"));
    text.push('\n');
    text
}

#[cfg(test)]
pub(crate) mod tests {
    use std::collections::BTreeMap;

    use super::*;
    use crate::bot::blueprint::{Does, Mode};

    /// A blueprint written by hand, shaped as D3B's automatic harvest will be: every kind of node, wire and variable.
    pub(crate) const HARVEST: &str = r#"{
  "schema": 1, "id": "automatic-harvest", "name": "Automatic harvest", "version": 1,
  "description": "Harvests what grows in a zone and empties its pods at the bank.",
  "variables": [
    {"id": "zone", "name": "Zone", "type": "zone", "per_character": true, "help": "Where to harvest."},
    {"id": "pods", "name": "Empty pods at", "type": "percentage", "default": 90, "bounds": [10, 100],
     "per_character": true},
    {"id": "harvests", "name": "Harvests", "type": "number", "default": 0, "memory": true}
  ],
  "graphs": [
    {"id": "events", "name": "Event graph",
     "nodes": [
       {"id": "n1", "kind": "on_start"},
       {"id": "n2", "get": "zone"},
       {"id": "n3", "kind": "harvest_until", "inputs": {"what": "all"}},
       {"id": "n4", "call": "empty_at_bank"},
       {"id": "n5", "kind": "life_below", "inputs": {"threshold": 40, "rearm": 60},
        "event": {"priority": 2, "mode": "interrupt_then_resume"}}
     ],
     "wires": [{"from": ["n1", "then"], "to": ["n3", "run"]}, {"from": ["n2", "value"], "to": ["n3", "zone"]}]},
    {"id": "empty_at_bank", "name": "Empty at the bank",
     "function": {"inputs": [{"id": "bank", "name": "Bank", "type": "bank"}]}}
  ],
  "layout": {"events": {"n3": [220, 0], "n1": [0, 0]}}
}"#;

    #[test]
    fn a_blueprint_reads_every_part_of_its_file() {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(blueprint.variables.len(), 3);
        let events = &blueprint.graphs[0];
        assert!(events.function.is_none() && blueprint.graphs[1].function.is_some());
        assert!(matches!(&events.nodes[3].does, Does::Call(function) if function.0 == "empty_at_bank"));
        assert_eq!(
            events.nodes[4].event.map(|event| event.mode),
            Some(Mode::InterruptThenResume)
        );
        assert_eq!(events.wires[1].to.1.0, "zone");
        assert_eq!(blueprint.layout.values().next().map(BTreeMap::len), Some(2));
    }

    #[test]
    fn a_blueprint_is_written_the_same_way_every_time() {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let written = write(&blueprint);
        assert_eq!(read(&written).ok().as_ref(), Some(&blueprint));
        assert_eq!(
            write(&read(&written).unwrap_or_else(|error| panic!("{error}"))),
            written
        );
        // Indented, the maps in the order of their keys, a new line at the end.
        assert!(
            written.starts_with(
                "{
  \"schema\": 1,
  \"id\": \"automatic-harvest\","
            ),
            "{written}"
        );
        assert!(written.find("\"rearm\"") < written.find("\"threshold\""), "{written}");
        assert!(written.find("\"n1\": [") < written.find("\"n3\": ["), "{written}");
        assert!(written.ends_with(
            "}
"
        ));
    }

    #[test]
    fn a_moved_node_changes_only_the_layout() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let before = write(&blueprint);
        for at in blueprint.layout.values_mut().flat_map(BTreeMap::values_mut) {
            at[0] += 40;
        }
        let after = write(&blueprint);
        let logic = |text: &str| {
            text.split(
                "
  \"layout\"",
            )
            .next()
            .map(str::to_owned)
        };
        assert_eq!(logic(&before), logic(&after));
        assert_ne!(before, after);
        assert!(
            before.ends_with(
                "]
    }
  }
}
"
            ),
            "the layout is the last key: {before}"
        );
    }

    #[test]
    fn a_newer_schema_is_refused_with_its_number() {
        let newer = HARVEST.replacen("\"schema\": 1", "\"schema\": 2", 1);
        assert!(matches!(read(&newer), Err(ReadError::Newer { found: 2 })));
    }

    #[test]
    fn a_file_without_its_schema_is_not_a_blueprint() {
        for text in [
            HARVEST.replacen("\"schema\": 1", "\"schema\": 0", 1),
            HARVEST.replacen("\"schema\": 1, ", "", 1),
        ] {
            let error = read(&text).err().map(|error| error.to_string()).unwrap_or_default();
            assert!(error.contains("its schema must be 1"), "{error}");
        }
    }

    #[test]
    fn a_field_this_version_does_not_know_is_refused() {
        let unknown = HARVEST.replacen("\"version\": 1,", "\"version\": 1,\n  \"colour\": \"red\",", 1);
        let error = read(&unknown).err().map(|error| error.to_string()).unwrap_or_default();
        assert!(error.contains("unknown field `colour`"), "{error}");
    }

    #[test]
    fn a_key_given_twice_or_unknown_anywhere_is_refused() {
        let twice = HARVEST.replacen("\"version\": 1,", "\"version\": 1, \"graphs\": [],", 1);
        let nested = HARVEST.replacen("\"get\": \"zone\"", "\"get\": \"zone\", \"position\": [0, 0]", 1);
        for (text, error) in [
            (twice, "duplicate field `graphs`"),
            (nested, "unknown field `position`"),
        ] {
            let found = read(&text).err().map(|error| error.to_string()).unwrap_or_default();
            assert!(found.contains(error) && found.contains("line"), "{found}");
        }
    }

    #[test]
    fn text_that_is_no_json_is_not_a_blueprint() {
        assert!(matches!(read("{\"schema\": 1,"), Err(ReadError::NotABlueprint(_))));
        assert!(matches!(read(""), Err(ReadError::NotABlueprint(_))));
    }
}
