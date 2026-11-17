//! A blueprint as its file holds it (`docs/bot.md`, "Storage", ADR 0015): its variables, its graphs (event graphs, and
//! functions with their inputs and outputs), each a list of nodes and the wires between their pins, and where its nodes
//! sit, kept apart from the logic under `layout` so that a moved node never changes what the blueprint does.

use std::collections::BTreeMap;
use std::num::{NonZeroU8, NonZeroU32};

use serde::{Deserialize, Serialize};

use super::variable::{Shape, Type, ValueType, Variable};

/// The `schema` this version writes and the newest it reads. A change to the file's shape bumps it, with a migration
/// and a test that reads the former shape (ADR 0015).
pub(crate) const SCHEMA: u32 = 1;

/// The id of something a blueprint names: itself, a variable, a graph, a node, a function's parameter, a node's kind or
/// pin. Ids stay as they are when the player renames what they name, so nothing that refers to them moves.
#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub(crate) struct Id(pub(crate) String);

impl Id {
    /// The longest id: enough for a slug of a name, short enough for a file name on every system.
    pub(crate) const LIMIT: usize = 64;

    /// Lowercase ASCII letters, digits, `_` and `-`, 1 to [`Id::LIMIT`] long, and no name Windows keeps for a device
    /// (`con`, `nul`, `com1`…), even with an extension: it holds as a file name on every system and reads the same in a
    /// diff.
    pub(crate) fn is_well_formed(&self) -> bool {
        let device = matches!(self.0.as_bytes(), b"con" | b"prn" | b"aux" | b"nul")
            || matches!(
                self.0.as_bytes(),
                [b'c', b'o', b'm', b'1'..=b'9'] | [b'l', b'p', b't', b'1'..=b'9']
            );
        !device
            && (1..=Self::LIMIT).contains(&self.0.len())
            && self
                .0
                .bytes()
                .all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit() || byte == b'_' || byte == b'-')
    }
}

/// A blueprint: a class that characters run (ADR 0014).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Blueprint {
    pub(crate) schema: u32,
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(default, skip_serializing_if = "String::is_empty")]
    pub(crate) description: String,
    /// Raised at each change of a given blueprint, so a copy can say its original has a newer version.
    pub(crate) version: NonZeroU32,
    /// The given blueprint this one was duplicated from, and its version then.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) based_on: Option<BasedOn>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) variables: Vec<Variable>,
    /// Events of the player's own (ADR 0016), each a node of the event graph, fired by Trigger at once or by a timer.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) events: Vec<OwnEvent>,
    pub(crate) graphs: Vec<Graph>,
    #[serde(default, skip_serializing_if = "BTreeMap::is_empty")]
    pub(crate) layout: Layout,
}

/// Where each node of each graph sits, by graph then node: what the editor moves, never what the blueprint does.
pub(crate) type Layout = BTreeMap<Id, BTreeMap<Id, [i32; 2]>>;

/// The blueprint a duplicate was made from.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct BasedOn {
    pub(crate) id: Id,
    pub(crate) version: NonZeroU32,
}

/// An event graph, or a function when it has a signature.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Graph {
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) function: Option<Signature>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) nodes: Vec<Node>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) wires: Vec<Wire>,
}

/// What a function receives and gives back; it also ends Done or Failed, as an action does.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Signature {
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) inputs: Vec<Parameter>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) outputs: Vec<Parameter>,
    /// Exits beside Done and Failed ("Full", "Empty"), each a flow output of its calls, by its id.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) exits: Vec<NamedExit>,
    /// Its own variables, from their defaults at each call and gone at its return.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) locals: Vec<Variable>,
    /// It computes values without flow, actions or waits, so its call is a value node, read when used.
    #[serde(default, skip_serializing_if = "std::ops::Not::not")]
    pub(crate) pure: bool,
}

/// A named exit of a function.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct NamedExit {
    pub(crate) id: Id,
    pub(crate) name: String,
}

/// An event of the player's own: its id, its name and the inputs its node gives, which Trigger passes.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct OwnEvent {
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) inputs: Vec<Parameter>,
}

/// One of a function's inputs or outputs.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Parameter {
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(rename = "type")]
    pub(crate) value_type: ValueType,
    #[serde(default, skip_serializing_if = "std::ops::Not::not")]
    pub(crate) list: bool,
    #[serde(default, skip_serializing_if = "std::ops::Not::not")]
    pub(crate) ranges: bool,
}

impl Parameter {
    /// Its whole type: its type, as a list or ranges or neither.
    pub(crate) fn of_type(&self) -> Type {
        Type::new(self.value_type, Shape::new(self.list, self.ranges))
    }
}

/// A node: what it does, the values typed on its inputs that no wire feeds, and, for an event, when it runs.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(try_from = "NodeFile", into = "NodeFile")]
pub(crate) struct Node {
    pub(crate) id: Id,
    pub(crate) does: Does,
    /// By pin, as JSON: the pin's type, which the catalogue knows, says how it reads.
    pub(crate) inputs: BTreeMap<Id, serde_json::Value>,
    pub(crate) event: Option<EventSettings>,
    pub(crate) options: Options,
}

/// What a node of a kind holds beyond its inputs (ADR 0016): the pins it makes and what of the blueprint it names. Each
/// is schema 2's, even those whose kinds come later, so that a kind added never changes the file's shape.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Options {
    /// The type its wildcard pins take (the owner, 2026-11-17: noted on the node, as Unreal keeps its pins' types), set
    /// by the first wire or value on one of them and dropped with the last.
    #[serde(default, rename = "type", skip_serializing_if = "Option::is_none")]
    pub(crate) value_type: Option<ValueType>,
    /// How many numbered pins it makes, `<pin>_1` to `<pin>_<count>`: Sequence's outputs, a list's inputs.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) count: Option<NonZeroU8>,
    /// Switch's cases, each a value of its type and an output `case_<n>`, from 1.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) cases: Vec<serde_json::Value>,
    /// Text's template, each of whose `{holes}` is an input of that name.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) text: Option<String>,
    /// An Expression's formula, read by Rust's parser (v2.8).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) expression: Option<String>,
    /// The event of mine it is or fires.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) event: Option<Id>,
    /// The variable it changes, whose type its wildcards take.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) variable: Option<Id>,
    /// The function's exit it returns by.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) exit: Option<Id>,
}

impl Options {
    fn is_empty(&self) -> bool {
        self == &Self::default()
    }
}

/// What a node does: one of the catalogue's, or a variable read or written, or a function of the blueprint called.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Does {
    Kind(Id),
    Get(Id),
    Set(Id),
    Call(Id),
}

/// A node as its file writes it: `kind`, `get`, `set` or `call`, exactly one of them.
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct NodeFile {
    id: Id,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    kind: Option<Id>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    get: Option<Id>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    set: Option<Id>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    call: Option<Id>,
    #[serde(default, skip_serializing_if = "BTreeMap::is_empty")]
    inputs: BTreeMap<Id, serde_json::Value>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    event: Option<EventSettings>,
    #[serde(default, skip_serializing_if = "Options::is_empty")]
    options: Options,
}

impl TryFrom<NodeFile> for Node {
    type Error = String;

    fn try_from(file: NodeFile) -> Result<Self, Self::Error> {
        let does = match (file.kind, file.get, file.set, file.call) {
            (Some(kind), None, None, None) => Does::Kind(kind),
            (None, Some(variable), None, None) => Does::Get(variable),
            (None, None, Some(variable), None) => Does::Set(variable),
            (None, None, None, Some(function)) => Does::Call(function),
            _ => {
                return Err(format!(
                    "node {} must have exactly one of kind, get, set and call",
                    file.id.0
                ));
            }
        };
        Ok(Self {
            id: file.id,
            does,
            inputs: file.inputs,
            event: file.event,
            options: file.options,
        })
    }
}

impl From<Node> for NodeFile {
    fn from(node: Node) -> Self {
        let (mut kind, mut get, mut set, mut call) = (None, None, None, None);
        match node.does {
            Does::Kind(id) => kind = Some(id),
            Does::Get(id) => get = Some(id),
            Does::Set(id) => set = Some(id),
            Does::Call(id) => call = Some(id),
        }
        Self {
            id: node.id,
            kind,
            get,
            set,
            call,
            inputs: node.inputs,
            event: node.event,
            options: node.options,
        }
    }
}

/// A wire from a node's output pin to another's input pin, each written `[node, pin]`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Wire {
    pub(crate) from: End,
    pub(crate) to: End,
}

/// A pin of a node: `[node, pin]`.
#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub(crate) struct End(pub(crate) Id, pub(crate) Id);

/// When an event runs against the flow at work (`docs/bot.md`, "How a blueprint runs"): 1 before 2, then its mode.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct EventSettings {
    pub(crate) priority: NonZeroU32,
    #[serde(default)]
    pub(crate) mode: Mode,
}

/// What an event does to the flow at work.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub(crate) enum Mode {
    /// It interrupts the action at work, which runs again once the event's flow ends.
    #[default]
    InterruptThenResume,
    /// It waits for the action at work to end.
    WaitForAction,
    /// It interrupts, and the main flow then starts again from its start.
    Restart,
    /// It runs beside the flow at work, for a graph that never acts.
    Parallel,
}

#[cfg(test)]
mod tests {
    use super::*;

    fn id(text: &str) -> Id {
        Id(text.to_owned())
    }

    #[test]
    fn an_id_is_lowercase_letters_digits_underscores_and_dashes() {
        assert!(id("harvest_until").is_well_formed());
        assert!(id("my-zone-2").is_well_formed());
        assert!(
            ["com", "com0", "col1", "console"]
                .iter()
                .all(|text| id(text).is_well_formed())
        );
        assert!(id(&"a".repeat(Id::LIMIT)).is_well_formed());
    }

    #[test]
    fn an_id_refuses_what_a_file_name_or_a_diff_would_not_hold() {
        for text in ["", "Pods", "pods %", "zone/1", "récolte", "a.json", "con\0"] {
            assert!(!id(text).is_well_formed(), "{text:?}");
        }
        assert!(!id(&"a".repeat(Id::LIMIT + 1)).is_well_formed());
    }

    #[test]
    fn a_node_does_exactly_one_thing() {
        let read = |text: &str| serde_json::from_str::<Node>(text).map_err(|error| error.to_string());
        assert_eq!(
            read(r#"{"id": "n1", "get": "pods"}"#).map(|node| node.does),
            Ok(Does::Get(id("pods")))
        );
        let error = read(r#"{"id": "n1", "get": "pods", "call": "bank"}"#)
            .err()
            .unwrap_or_default();
        assert!(
            error.contains("node n1 must have exactly one of kind, get, set and call"),
            "{error}"
        );
        assert!(read(r#"{"id": "n1"}"#).is_err());
    }

    #[test]
    fn a_node_notes_the_type_of_its_wildcards_in_its_options_and_omits_none() {
        let read = |text: &str| serde_json::from_str::<Node>(text).map_err(|error| error.to_string());
        let typed = read(r#"{"id": "n1", "kind": "equal", "options": {"type": "zone"}}"#);
        assert_eq!(
            typed.as_ref().map(|node| node.options.value_type),
            Ok(Some(ValueType::Zone))
        );
        let written = typed.map(|node| serde_json::to_string(&node).unwrap_or_default());
        assert_eq!(
            written.as_deref(),
            Ok(r#"{"id":"n1","kind":"equal","options":{"type":"zone"}}"#)
        );
        let untyped =
            read(r#"{"id": "n1", "kind": "equal"}"#).map(|node| serde_json::to_string(&node).unwrap_or_default());
        assert_eq!(untyped.as_deref(), Ok(r#"{"id":"n1","kind":"equal"}"#));
        let unknown = read(r#"{"id": "n1", "kind": "equal", "options": {"colour": "red"}}"#);
        assert!(unknown.is_err_and(|error| error.contains("unknown field `colour`")));
    }

    #[test]
    fn a_node_reads_every_option_and_writes_them_back_as_they_were() {
        let all = r#"{"id":"n1","kind":"k","options":{"type":"job","count":3,"cases":[24,28],"text":"{count} done","expression":"level >= 50","event":"empty","variable":"pods","exit":"full"}}"#;
        let node = serde_json::from_str::<Node>(all).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(node.options.count.map(NonZeroU8::get), Some(3));
        assert_eq!(serde_json::to_string(&node).ok().as_deref(), Some(all));
        for refused in [r#"{"count": 0}"#, r#"{"count": 256}"#, r#"{"cases": 24}"#] {
            let text = format!(r#"{{"id": "n1", "kind": "k", "options": {refused}}}"#);
            assert!(serde_json::from_str::<Node>(&text).is_err(), "{refused}");
        }
    }

    #[test]
    fn a_blueprint_reads_its_own_events_and_a_functions_exits_locals_and_purity() {
        let text = r#"{"schema": 1, "id": "b", "name": "B", "version": 1,
          "events": [{"id": "empty", "name": "Zone empty", "inputs": [{"id": "zone", "name": "Zone", "type": "zone"}]}],
          "graphs": [{"id": "full", "name": "Full", "function": {"exits": [{"id": "full", "name": "Full"}],
            "locals": [{"id": "tries", "name": "Tries", "type": "number", "default": 0}], "pure": true}}]}"#;
        let blueprint: Blueprint = serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(blueprint.events[0].inputs[0].id, id("zone"));
        let signature = blueprint.graphs[0]
            .function
            .clone()
            .unwrap_or_else(|| panic!("a function"));
        assert!(signature.pure && signature.exits[0].id == id("full") && signature.locals[0].id == id("tries"));
        let written = serde_json::to_string(&Signature {
            pure: false,
            ..signature
        });
        assert!(written.is_ok_and(|it| !it.contains("pure")), "not pure, not written");
    }

    #[test]
    fn an_event_interrupts_then_resumes_unless_told_otherwise() {
        let event: EventSettings = serde_json::from_str(r#"{"priority": 2}"#).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(event.mode, Mode::InterruptThenResume);
        assert!(serde_json::from_str::<EventSettings>(r#"{"priority": 0}"#).is_err());
        assert!(serde_json::from_str::<EventSettings>(r#"{"priority": 1, "mode": "sometimes"}"#).is_err());
    }
}
