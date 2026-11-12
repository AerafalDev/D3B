//! The pins' audit (`docs/protocol.md`, "A new build"): every message our `.proto` files declare, reached from a
//! build's pins and through the messages their fields hold, must exist in the build's schema, which the extractor
//! writes to `artifacts/protocol/<build>.json`, with the same field numbers, kinds, lists, presence and oneofs.
//! The maintainer runs it before committing a build's pins; CI has no schema, so it is ignored there.

use std::collections::{HashMap, VecDeque};
use std::path::PathBuf;

use serde::Deserialize;

use super::pins::Pins;

/// Our messages as `build.rs` describes them from our `.proto` files.
const OURS: &str = include_str!(concat!(env!("OUT_DIR"), "/protocol-shapes.txt"));

/// The format of the extractor's schema this audit reads (`tools/extractor/src/Protocol/ProtocolSchema.cs`).
const SCHEMA_FORMAT: u32 = 1;

/// A field as both sides are compared: its number, kind, `repeated` or `optional`, its oneof and the type it holds.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
struct Field {
    number: u32,
    kind: String,
    label: Option<String>,
    oneof: Option<String>,
    #[serde(rename = "type")]
    of: Option<String>,
}

/// Messages and enums by name.
#[derive(Debug, Default)]
struct Shapes {
    messages: HashMap<String, Vec<Field>>,
    enums: HashMap<String, Vec<i64>>,
}

/// The extractor's schema of a build, as much of it as the audit reads.
#[derive(Deserialize)]
struct Schema {
    build: String,
    format: u32,
    sha256: String,
    messages: Vec<SchemaMessage>,
    enums: Vec<SchemaEnum>,
}

#[derive(Deserialize)]
struct SchemaMessage {
    name: String,
    fields: Vec<Field>,
}

#[derive(Deserialize)]
struct SchemaEnum {
    name: String,
    values: Vec<i64>,
}

impl Schema {
    fn shapes(self) -> Shapes {
        Shapes {
            messages: self
                .messages
                .into_iter()
                .map(|message| (message.name, message.fields))
                .collect(),
            enums: self.enums.into_iter().map(|kind| (kind.name, kind.values)).collect(),
        }
    }
}

/// Our shapes from `build.rs`'s lines.
fn ours(text: &str) -> Shapes {
    let mut shapes = Shapes::default();
    let mut current = None;
    let none = |part: &str| (part != "-").then(|| part.to_owned());
    for line in text.lines() {
        let parts: Vec<&str> = line.split(' ').collect();
        match parts.as_slice() {
            ["message", name] => {
                shapes.messages.insert((*name).to_owned(), Vec::new());
                current = Some((*name).to_owned());
            }
            ["field", number, kind, label, oneof, of] => {
                let field = Field {
                    number: number.parse().unwrap_or_else(|error| panic!("{line}: {error}")),
                    kind: (*kind).to_owned(),
                    label: none(label),
                    oneof: none(oneof),
                    of: none(of),
                };
                let message = current.as_ref().and_then(|name| shapes.messages.get_mut(name));
                message
                    .unwrap_or_else(|| panic!("{line}: a field of no message"))
                    .push(field);
            }
            ["enum", name, values @ ..] => {
                let values = values
                    .iter()
                    .map(|value| value.parse().unwrap_or_else(|error| panic!("{error}")));
                shapes.enums.insert((*name).to_owned(), values.collect());
            }
            _ => panic!("{line}: not a line build.rs writes"),
        }
    }
    shapes
}

/// What differs between our messages and the build's, from the `pinned` pairs (our name, the build's) and the pairs
/// their fields lead to; empty when nothing does.
fn audit(ours: &Shapes, theirs: &Shapes, pinned: &[(&str, &str)]) -> Vec<String> {
    let mut problems = Vec::new();
    let mut paired: HashMap<String, String> = HashMap::new();
    let mut pending: VecDeque<(String, String)> = pinned
        .iter()
        .map(|(our, their)| ((*our).to_owned(), (*their).to_owned()))
        .collect();
    while let Some((our, their)) = pending.pop_front() {
        if let Some(already) = paired.get(&our) {
            if *already != their {
                problems.push(format!("{our} is paired with {already} and with {their}"));
            }
            continue;
        }
        paired.insert(our.clone(), their.clone());
        let (Some(our_fields), Some(their_fields)) = (ours.messages.get(&our), theirs.messages.get(&their)) else {
            problems.push(format!("{our} or {their} is no message"));
            continue;
        };
        let mut oneofs: HashMap<&str, &str> = HashMap::new();
        for field in our_fields {
            let at = format!("{our}.{} ({their})", field.number);
            let Some(other) = their_fields.iter().find(|other| other.number == field.number) else {
                problems.push(format!("{at}: no such field in the build"));
                continue;
            };
            problems.extend(compared(&at, field, other, &mut oneofs));
            match (field.kind.as_str(), &field.of, &other.of) {
                ("message", Some(our_type), Some(their_type)) if other.kind == "message" => {
                    pending.push_back((our_type.clone(), their_type.clone()));
                }
                ("enum", Some(our_type), Some(their_type)) if other.kind == "enum" => {
                    let (ours, theirs) = (ours.enums.get(our_type), theirs.enums.get(their_type));
                    if !ours
                        .zip(theirs)
                        .is_some_and(|(ours, theirs)| ours.iter().all(|value| theirs.contains(value)))
                    {
                        problems.push(format!("{at}: the enum {our_type} holds a value {their_type} lacks"));
                    }
                }
                _ => {}
            }
        }
    }
    problems
}

/// What differs between one field of ours and the build's, noting which of the build's oneofs each of ours is.
fn compared<'a>(at: &str, field: &'a Field, other: &'a Field, oneofs: &mut HashMap<&'a str, &'a str>) -> Vec<String> {
    let mut problems = Vec::new();
    // An enum we do not need is read as its number (docs/protocol.md, "Decoding").
    if field.kind != other.kind && (field.kind.as_str(), other.kind.as_str()) != ("int32", "enum") {
        problems.push(format!("{at}: {} where the build has {}", field.kind, other.kind));
    }
    let repeated = |field: &Field| field.label.as_deref() == Some("repeated");
    if repeated(field) != repeated(other) {
        problems.push(format!("{at}: repeated on one side only"));
    }
    // A scalar's presence: without it, prost drops a 0 the build sends and the frame looks "read".
    let present = |field: &Field| {
        field.label.as_deref() == Some("optional") || (field.oneof.is_some() && field.kind != "message")
    };
    if field.kind != "message" && present(field) != present(other) {
        problems.push(format!("{at}: its presence is not the build's"));
    }
    if let Some(ours) = &field.oneof {
        match &other.oneof {
            None => problems.push(format!("{at}: in a oneof, where the build has none")),
            Some(theirs) => {
                if oneofs
                    .insert(ours, theirs)
                    .is_some_and(|before| before != theirs.as_str())
                {
                    problems.push(format!("{at}: the oneof {ours} spans two of the build's"));
                }
            }
        }
    }
    problems
}

#[test]
fn our_proto_files_describe_every_message_of_ours() {
    let ours = ours(OURS);

    for &readable in super::message::READABLE {
        assert!(ours.messages.contains_key(readable), "{readable}");
    }
    let field = |number, kind: &str, label: Option<&str>, oneof: Option<&str>, of: Option<&str>| Field {
        number,
        kind: kind.to_owned(),
        label: label.map(str::to_owned),
        oneof: oneof.map(str::to_owned),
        of: of.map(str::to_owned),
    };
    for (message, expected) in [
        ("ActorDisposition", field(3, "int32", Some("optional"), None, None)),
        (
            "CharacterSelectionEvent.Success",
            field(1, "message", None, None, Some("Character")),
        ),
        (
            "Characteristic",
            field(
                3,
                "message",
                None,
                Some("Characteristic.kind"),
                Some("CharacteristicValue"),
            ),
        ),
    ] {
        assert!(ours.messages[message].contains(&expected), "{message}: {expected:?}");
    }
    assert_eq!(ours.enums["ContextCreationEvent.GameContext"], [0, 1]);
}

#[test]
#[ignore = "needs the extractor's schema of each pinned build in artifacts/protocol/, or in the folder D3B_SCHEMAS names"]
fn the_pins_hold_against_their_builds_schema() {
    let folder = std::env::var_os("D3B_SCHEMAS").map_or_else(
        || PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../artifacts/protocol"),
        PathBuf::from,
    );
    for build in Pins::builds() {
        let pins = Pins::embedded(build)
            .and_then(Result::ok)
            .unwrap_or_else(|| panic!("the pins of {build}"));
        let path = folder.join(format!("{build}.json"));
        let text = std::fs::read_to_string(&path).unwrap_or_else(|error| panic!("{}: {error}", path.display()));
        let schema: Schema = serde_json::from_str(&text).unwrap_or_else(|error| panic!("{}: {error}", path.display()));

        assert_eq!((schema.format, schema.build.as_str()), (SCHEMA_FORMAT, build));
        assert_eq!(
            schema.sha256,
            pins.schema_sha256(),
            "{build}: the pins were made against another schema"
        );
        let pinned: Vec<(&str, &str)> = pins.pinned().collect();
        let problems = audit(&ours(OURS), &schema.shapes(), &pinned);
        assert!(problems.is_empty(), "{build}:\n{}", problems.join("\n"));
        eprintln!(
            "{build}: {} messages pinned, their fields as the schema has them",
            pinned.len()
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The build's side, written as the extractor writes it.
    fn theirs(json: &str) -> Shapes {
        let messages: Vec<SchemaMessage> = serde_json::from_str(json).unwrap_or_else(|error| panic!("{error}"));
        Schema {
            build: String::new(),
            format: SCHEMA_FORMAT,
            sha256: String::new(),
            messages,
            enums: vec![SchemaEnum {
                name: "abe".into(),
                values: vec![0, 1, 2],
            }],
        }
        .shapes()
    }

    const OURS: &str = "message Map
field 1 int64 - - -
field 2 message repeated - Actor
field 3 int32 optional - -
field 4 enum - - Map.Kind
enum Map.Kind 0 1
message Actor
field 1 int64 - - -
field 2 message - Actor.kind Disposition
field 3 int32 - Actor.kind -
message Disposition
field 3 int32 - - -";

    const THEIRS: &str = r#"[
        {"name": "abc", "fields": [{"number": 1, "kind": "int64"}, {"number": 2, "kind": "message", "label": "repeated", "type": "abd"},
            {"number": 3, "kind": "int32", "label": "optional"}, {"number": 4, "kind": "enum", "type": "abe"}, {"number": 5, "kind": "bool"}]},
        {"name": "abd", "fields": [{"number": 1, "kind": "int64"}, {"number": 2, "kind": "message", "type": "abf", "oneof": "abd.x"},
            {"number": 3, "kind": "int32", "oneof": "abd.x"}]},
        {"name": "abf", "fields": [{"number": 3, "kind": "enum", "type": "abe"}]}
    ]"#;

    fn problems(ours_text: &str, theirs_json: &str, pinned: &[(&str, &str)]) -> Vec<String> {
        audit(&ours(ours_text), &theirs(theirs_json), pinned)
    }

    #[test]
    fn our_messages_as_the_build_has_them_pass() {
        assert_eq!(problems(OURS, THEIRS, &[("Map", "abc")]), Vec::<String>::new());
    }

    #[test]
    fn each_difference_is_named() {
        for (ours, needle) in [
            (
                OURS.replace("field 1 int64 - - -\nfield 2", "field 6 int64 - - -\nfield 2"),
                "Map.6 (abc): no such field",
            ),
            (
                OURS.replace("field 1 int64 - - -\nfield 2", "field 1 int32 - - -\nfield 2"),
                "int32 where the build has int64",
            ),
            (
                OURS.replace("field 2 message repeated", "field 2 message -"),
                "repeated on one side only",
            ),
            (
                OURS.replace("field 3 int32 optional - -", "field 3 int32 - - -"),
                "presence is not the build's",
            ),
            (
                OURS.replace("field 3 int32 - Actor.kind -", "field 3 int32 - - -"),
                "presence is not the build's",
            ),
            (
                OURS.replace(
                    "field 1 int64 - - -\nfield 2 message - Actor.kind",
                    "field 1 int64 - Actor.kind -\nfield 2 message - Actor.kind",
                ),
                "in a oneof, where the build has none",
            ),
            (
                OURS.replace("enum Map.Kind 0 1", "enum Map.Kind 0 3"),
                "holds a value abe lacks",
            ),
        ] {
            let found = problems(&ours, THEIRS, &[("Map", "abc")]);

            assert!(
                found.iter().any(|problem| problem.contains(needle)),
                "{needle}: {found:?}"
            );
        }
    }

    #[test]
    fn a_message_of_ours_plain_where_the_builds_is_in_a_oneof_passes() {
        // As our `Character.basics`: a message's presence is kept either way, prost writing back what it read.
        let ours = "message Actor
field 2 message - - Disposition
message Disposition";

        assert_eq!(problems(ours, THEIRS, &[("Actor", "abd")]), Vec::<String>::new());
        let scalar = "message Actor
field 3 int32 - - -";
        assert!(
            !problems(scalar, THEIRS, &[("Actor", "abd")]).is_empty(),
            "a scalar's presence is compared"
        );
    }

    #[test]
    fn an_enum_read_as_its_number_passes() {
        let ours = "message Disposition\nfield 3 int32 - - -";

        assert_eq!(problems(ours, THEIRS, &[("Disposition", "abf")]), Vec::<String>::new());
    }

    #[test]
    fn a_oneof_of_ours_over_two_of_the_builds_fails() {
        let theirs = THEIRS.replace(
            r#"{"number": 3, "kind": "int32", "oneof": "abd.x"}"#,
            r#"{"number": 3, "kind": "int32", "oneof": "abd.y"}"#,
        );

        let found = problems(OURS, &theirs, &[("Map", "abc")]);

        assert!(found.iter().any(|problem| problem.contains("spans two")), "{found:?}");
    }

    #[test]
    fn a_message_paired_twice_otherwise_or_missing_fails() {
        let twice = problems(OURS, THEIRS, &[("Map", "abc"), ("Actor", "abd"), ("Actor", "abf")]);
        let missing = problems(OURS, THEIRS, &[("Map", "zzz")]);

        assert!(
            twice
                .iter()
                .any(|problem| problem.contains("Actor is paired with abd and with abf")),
            "{twice:?}"
        );
        assert!(
            missing.iter().any(|problem| problem.contains("is no message")),
            "{missing:?}"
        );
    }
}
