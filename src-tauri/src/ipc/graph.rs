//! A blueprint's graphs as the editor draws them (`docs/bot.md`, "In the app"): each node with what it does, its family
//! and the step that makes it run, its pins from the catalogue, the values typed on them and where it sits; and the
//! wires. Read-only for now: the editor's changes come with step 3's PR 13.

use std::collections::BTreeMap;

use serde::Serialize;
use specta::Type;
use tauri::Manager;

use super::blueprints::PlayersFiles;
use crate::bot::{self, Blueprint, Carries, Does, Family, Mode, Problem, Side, ValueType};
use crate::storage::Store;

/// A blueprint's graphs, and its problems, each where the editor marks it.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
pub struct BlueprintGraphs {
    pub id: String,
    pub graphs: Vec<GraphView>,
    pub problems: Vec<ProblemView>,
}

/// A problem of a blueprint (`bot::Problem`), its kind for the interface to word, and where it is: its graph, its node
/// and pin, and the variable, function, kind or other id it names.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct ProblemView {
    pub kind: ProblemKind,
    pub graph: Option<String>,
    pub node: Option<String>,
    pub pin: Option<String>,
    pub subject: Option<String>,
}

/// The kinds of `bot::Problem`, by name.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum ProblemKind {
    MalformedId,
    VariableTwice,
    GraphTwice,
    NodeTwice,
    ParameterTwice,
    DefaultNotOfType,
    WrongBounds,
    DefaultOutOfBounds,
    NoSuchVariable,
    NoSuchFunction,
    WireToNoNode,
    WireTwice,
    UnknownKind,
    OutOfPlace,
    EventSettingsOnNoEvent,
    NoSuchInput,
    ValueNotOfType,
    WireToNoPin,
    WireTheWrongWay,
    WireOfAnotherType,
    OutputWiredTwice,
    InputFedTwice,
    InputWithoutValue,
    EventTwice,
}

impl From<&Problem> for ProblemView {
    fn from(problem: &Problem) -> Self {
        use ProblemKind as K;
        let text = |id: &bot::Id| Some(id.0.clone());
        let (kind, graph, node, pin, subject) = match problem {
            Problem::MalformedId { id } => (K::MalformedId, None, None, None, text(id)),
            Problem::VariableTwice { variable } => (K::VariableTwice, None, None, None, text(variable)),
            Problem::GraphTwice { graph } => (K::GraphTwice, text(graph), None, None, None),
            Problem::NodeTwice { graph, node } => (K::NodeTwice, text(graph), text(node), None, None),
            Problem::ParameterTwice { graph, parameter } => {
                (K::ParameterTwice, text(graph), None, None, text(parameter))
            }
            Problem::DefaultNotOfType { variable } => (K::DefaultNotOfType, None, None, None, text(variable)),
            Problem::WrongBounds { variable } => (K::WrongBounds, None, None, None, text(variable)),
            Problem::DefaultOutOfBounds { variable } => (K::DefaultOutOfBounds, None, None, None, text(variable)),
            Problem::NoSuchVariable { graph, node, variable } => {
                (K::NoSuchVariable, text(graph), text(node), None, text(variable))
            }
            Problem::NoSuchFunction { graph, node, function } => {
                (K::NoSuchFunction, text(graph), text(node), None, text(function))
            }
            Problem::WireToNoNode { graph, node } => (K::WireToNoNode, text(graph), None, None, text(node)),
            Problem::WireTwice { graph, from, to } => (K::WireTwice, text(graph), text(from), None, text(to)),
            Problem::UnknownKind { graph, node, kind } => (K::UnknownKind, text(graph), text(node), None, text(kind)),
            Problem::OutOfPlace { graph, node } => (K::OutOfPlace, text(graph), text(node), None, None),
            Problem::EventSettingsOnNoEvent { graph, node } => {
                (K::EventSettingsOnNoEvent, text(graph), text(node), None, None)
            }
            Problem::NoSuchInput { graph, node, pin } => (K::NoSuchInput, text(graph), text(node), text(pin), None),
            Problem::ValueNotOfType { graph, node, pin } => {
                (K::ValueNotOfType, text(graph), text(node), text(pin), None)
            }
            Problem::WireToNoPin { graph, node, pin } => (K::WireToNoPin, text(graph), text(node), text(pin), None),
            Problem::WireTheWrongWay { graph, node, pin } => {
                (K::WireTheWrongWay, text(graph), text(node), text(pin), None)
            }
            Problem::WireOfAnotherType { graph, from, to } => (
                K::WireOfAnotherType,
                text(graph),
                text(&from.0),
                text(&from.1),
                text(&to.0),
            ),
            Problem::OutputWiredTwice { graph, node, pin } => {
                (K::OutputWiredTwice, text(graph), text(node), text(pin), None)
            }
            Problem::InputFedTwice { graph, node, pin } => (K::InputFedTwice, text(graph), text(node), text(pin), None),
            Problem::InputWithoutValue { graph, node, pin } => {
                (K::InputWithoutValue, text(graph), text(node), text(pin), None)
            }
            Problem::EventTwice { graph, node } => (K::EventTwice, text(graph), text(node), None, None),
        };
        Self {
            kind,
            graph,
            node,
            pin,
            subject,
        }
    }
}

/// An event's priority (1 before 2) and its mode against the flow at work.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
pub struct EventView {
    pub priority: u32,
    pub mode: EventMode,
}

/// What an event does to the flow at work (`bot::Mode`).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum EventMode {
    InterruptThenResume,
    WaitForAction,
    Restart,
    Parallel,
}

/// One of a function's inputs or outputs.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct ParameterView {
    pub id: String,
    pub name: String,
    pub value_type: PinCarries,
}

/// An event graph, or a function when it has a signature.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
pub struct GraphView {
    pub id: String,
    pub name: String,
    /// Whether it is a function rather than an event graph.
    pub function: bool,
    /// A function's inputs and outputs; none for an event graph.
    pub inputs: Vec<ParameterView>,
    pub outputs: Vec<ParameterView>,
    pub nodes: Vec<NodeView>,
    pub wires: Vec<WireView>,
}

/// What a node does.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "does", rename_all = "camelCase")]
pub enum NodeDoes {
    /// One of the catalogue's kinds, by id; its sentence is the interface's `node.kinds.<kind>`.
    Kind {
        kind: String,
    },
    Get {
        variable: String,
    },
    Set {
        variable: String,
    },
    Call {
        function: String,
    },
}

/// A node of a graph.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct NodeView {
    pub id: String,
    #[serde(flatten)]
    pub does: NodeDoes,
    /// `event`, `action`, `flow`, `query`, `function`, `variable` (a get or a set) or `call`; `null` for a kind this
    /// version does not know, kept as it is.
    pub family: Option<NodeFamily>,
    /// The roadmap step from which the bot does it in the game; the editor marks it until then (the owner, 2026-11-14).
    pub from_step: Option<u8>,
    /// Its pins, from the catalogue or from its variable or function; none when what it does is unknown.
    pub pins: Vec<PinView>,
    /// The values typed on its inputs, as JSON, by pin.
    pub inputs: BTreeMap<String, String>,
    /// Where it sits, `[x, y]`; `None` when the file does not say, and the editor places it.
    pub position: Option<[i32; 2]>,
    /// An event's priority and mode, as the file sets them.
    pub event: Option<EventView>,
}

/// A pin of a node.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct PinView {
    pub id: String,
    pub side: PinSide,
    pub carries: PinCarries,
    /// The value an input no wire feeds holds by default, as JSON.
    pub default: Option<String>,
}

/// A node's family, which colours its icon; a get or a set is a `variable`, a call a `call`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum NodeFamily {
    Event,
    Action,
    Flow,
    Query,
    Function,
    Variable,
    Call,
}

/// Which way a pin goes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum PinSide {
    In,
    Out,
}

/// What a pin carries: the flow, or a value of a type.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum PinCarries {
    Exec,
    Bool,
    Number,
    Percentage,
    Text,
    Zone,
    Route,
    Bank,
    Items,
    Job,
}

impl From<Carries> for PinCarries {
    fn from(carries: Carries) -> Self {
        match carries {
            Carries::Exec => Self::Exec,
            Carries::Value(value_type) => match value_type {
                ValueType::Bool => Self::Bool,
                ValueType::Number => Self::Number,
                ValueType::Percentage => Self::Percentage,
                ValueType::Text => Self::Text,
                ValueType::Zone => Self::Zone,
                ValueType::Route => Self::Route,
                ValueType::Bank => Self::Bank,
                ValueType::Items => Self::Items,
                ValueType::Job => Self::Job,
            },
        }
    }
}

/// Why a blueprint's graphs are not given.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum GraphsError {
    /// No blueprint has this id: its file is gone, or no longer reads.
    NoSuchBlueprint,
    /// A bug of this version, said as it came: D3B's own blueprints do not read, or the read panicked.
    Bug { reason: String },
}

/// A wire from a node's output to another's input: `[node, pin]` each.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct WireView {
    pub from: [String; 2],
    pub to: [String; 2],
}

fn family(node: &bot::Node) -> (Option<NodeFamily>, Option<u8>) {
    match &node.does {
        Does::Kind(kind) => match bot::kind(&kind.0) {
            Some(kind) => {
                let family = match kind.family {
                    Family::Event => NodeFamily::Event,
                    Family::Action => NodeFamily::Action,
                    Family::Flow => NodeFamily::Flow,
                    Family::Query => NodeFamily::Query,
                    Family::Function => NodeFamily::Function,
                };
                (Some(family), kind.from_step)
            }
            None => (None, None),
        },
        Does::Get(_) | Does::Set(_) => (Some(NodeFamily::Variable), None),
        // A call of a function the blueprint lacks is a problem of its own; it is drawn as a call still.
        Does::Call(_) => (Some(NodeFamily::Call), None),
    }
}

fn parameters(parameters: Option<&Vec<bot::Parameter>>) -> Vec<ParameterView> {
    let views = parameters.into_iter().flatten().map(|parameter| ParameterView {
        id: parameter.id.0.clone(),
        name: parameter.name.clone(),
        value_type: Carries::Value(parameter.value_type).into(),
    });
    views.collect()
}

/// `blueprint`'s graphs as the editor draws them.
pub(crate) fn graphs(blueprint: &Blueprint) -> BlueprintGraphs {
    let graphs = blueprint.graphs.iter().map(|graph| {
        let layout = blueprint.layout.get(&graph.id);
        let nodes = graph.nodes.iter().map(|node| {
            let (family, from_step) = family(node);
            let pins = bot::pins(blueprint, graph, node).unwrap_or_default();
            NodeView {
                id: node.id.0.clone(),
                does: match &node.does {
                    Does::Kind(kind) => NodeDoes::Kind { kind: kind.0.clone() },
                    Does::Get(variable) => NodeDoes::Get {
                        variable: variable.0.clone(),
                    },
                    Does::Set(variable) => NodeDoes::Set {
                        variable: variable.0.clone(),
                    },
                    Does::Call(function) => NodeDoes::Call {
                        function: function.0.clone(),
                    },
                },
                family,
                from_step,
                pins: pins
                    .into_iter()
                    .map(|pin| PinView {
                        id: pin.id,
                        side: if pin.side == Side::In {
                            PinSide::In
                        } else {
                            PinSide::Out
                        },
                        carries: pin.carries.into(),
                        default: pin.default.map(|value| value.to_string()),
                    })
                    .collect(),
                inputs: node
                    .inputs
                    .iter()
                    .map(|(pin, value)| (pin.0.clone(), value.to_string()))
                    .collect(),
                position: layout.and_then(|nodes| nodes.get(&node.id)).copied(),
                event: node.event.map(|event| EventView {
                    priority: event.priority.get(),
                    mode: match event.mode {
                        Mode::InterruptThenResume => EventMode::InterruptThenResume,
                        Mode::WaitForAction => EventMode::WaitForAction,
                        Mode::Restart => EventMode::Restart,
                        Mode::Parallel => EventMode::Parallel,
                    },
                }),
            }
        });
        GraphView {
            id: graph.id.0.clone(),
            name: graph.name.clone(),
            function: graph.function.is_some(),
            inputs: parameters(graph.function.as_ref().map(|signature| &signature.inputs)),
            outputs: parameters(graph.function.as_ref().map(|signature| &signature.outputs)),
            nodes: nodes.collect(),
            wires: graph
                .wires
                .iter()
                .map(|wire| WireView {
                    from: [wire.from.0.0.clone(), wire.from.1.0.clone()],
                    to: [wire.to.0.0.clone(), wire.to.1.0.clone()],
                })
                .collect(),
        }
    });
    let problems = bot::problems(blueprint).iter().map(ProblemView::from).collect();
    BlueprintGraphs {
        id: blueprint.id.0.clone(),
        graphs: graphs.collect(),
        problems,
    }
}

/// The blueprint `id`, D3B's or mine; an error only for a bug, D3B's own not reading.
fn find(store: Option<&Store>, id: &str) -> Result<Option<Blueprint>, GraphsError> {
    let given = bot::given().map_err(|error| GraphsError::Bug {
        reason: error.to_string(),
    })?;
    let mine = store.map(|store| store.blueprints().read).unwrap_or_default();
    Ok(given.into_iter().chain(mine).find(|blueprint| blueprint.id.0 == id))
}

/// The graphs of the blueprint `id`, or why not.
#[tauri::command]
#[specta::specta]
pub async fn blueprint_graphs(app: tauri::AppHandle, id: String) -> Result<BlueprintGraphs, GraphsError> {
    let read = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        find(files.store(), &id)?
            .map(|blueprint| graphs(&blueprint))
            .ok_or(GraphsError::NoSuchBlueprint)
    });
    read.await.unwrap_or_else(|error| {
        Err(GraphsError::Bug {
            reason: error.to_string(),
        })
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn harvest() -> Blueprint {
        bot::given()
            .unwrap_or_default()
            .into_iter()
            .next()
            .unwrap_or_else(|| panic!("no given blueprint"))
    }

    #[test]
    fn a_given_blueprint_draws_its_nodes_with_their_pins_family_step_and_place() {
        let drawn = graphs(&harvest());
        let events = &drawn.graphs[0];
        assert!(!events.function);
        let node = |id: &str| {
            events
                .nodes
                .iter()
                .find(|node| node.id == id)
                .unwrap_or_else(|| panic!("no {id}"))
        };
        let harvest = node("harvest");
        assert_eq!(
            harvest.does,
            NodeDoes::Kind {
                kind: "harvest_until".into()
            }
        );
        assert_eq!(
            (harvest.family, harvest.from_step, harvest.position),
            (Some(NodeFamily::Action), Some(5), Some([560, 0]))
        );
        let pods = harvest
            .pins
            .iter()
            .find(|pin| pin.id == "pods")
            .unwrap_or_else(|| panic!("no pods"));
        assert_eq!(
            (pods.side, pods.carries, pods.default.as_deref()),
            (PinSide::In, PinCarries::Percentage, Some("90"))
        );
        assert_eq!(harvest.inputs.get("what").map(String::as_str), Some("\"all\""));
        let zone = node("zone");
        assert_eq!(
            (zone.family, &zone.does),
            (
                Some(NodeFamily::Variable),
                &NodeDoes::Get {
                    variable: "zone".into()
                }
            )
        );
        assert_eq!(zone.pins[0].carries, PinCarries::Zone);
        assert!(events.wires.contains(&WireView {
            from: ["deposit".into(), "done".into()],
            to: ["go".into(), "run".into()]
        }));
    }

    #[test]
    fn a_kind_this_version_does_not_know_is_drawn_without_family_or_pins() {
        let mut blueprint = harvest();
        blueprint.graphs[0].nodes[0].does = Does::Kind(bot::Id("teleport".into()));
        blueprint.layout.clear();
        let drawn = graphs(&blueprint);
        let unknown = &drawn.graphs[0].nodes[0];
        assert_eq!((unknown.family, unknown.pins.len(), unknown.position), (None, 0, None));
    }

    #[test]
    fn a_blueprint_is_found_among_d3bs_and_mine_and_none_else() {
        let folder = Folder::new("ipc-graph");
        let store = Store::new(folder.path().to_path_buf());
        let copy = store.duplicate(
            "hunt",
            bot::Texts {
                name: "Ma chasse".into(),
                ..bot::Texts::default()
            },
        );
        let copy = copy.unwrap_or_else(|error| panic!("{error}"));
        let found = |store: Option<&Store>, id: &str| find(store, id).unwrap_or_else(|error| panic!("{error:?}"));
        assert!(found(Some(&store), "automatic-harvest").is_some());
        assert_eq!(
            found(Some(&store), &copy.0).map(|it| it.name),
            Some("Ma chasse".to_owned())
        );
        assert!(found(Some(&store), "teleport").is_none());
        assert!(found(None, &copy.0).is_none());
    }

    #[test]
    fn a_function_graph_is_drawn_as_one_with_its_entrys_pins_and_its_own_layout() {
        let mut blueprint = bot::read(bot::HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let entry =
            serde_json::from_str(r#"{"id": "entry", "kind": "entry"}"#).unwrap_or_else(|error| panic!("{error}"));
        blueprint.graphs[1].nodes.push(entry);
        blueprint.layout.insert(
            bot::Id("empty_at_bank".into()),
            [(bot::Id("entry".into()), [7, 8])].into(),
        );
        let drawn = graphs(&blueprint);
        let (events, function) = (&drawn.graphs[0], &drawn.graphs[1]);
        assert!(!events.function && function.function);
        let entry = &function.nodes[0];
        assert_eq!(
            (entry.family, entry.position),
            (Some(NodeFamily::Function), Some([7, 8]))
        );
        let bank = entry
            .pins
            .iter()
            .find(|pin| pin.id == "bank")
            .map(|pin| (pin.side, pin.carries));
        assert_eq!(bank, Some((PinSide::Out, PinCarries::Bank)));
        assert_eq!(
            events
                .nodes
                .iter()
                .find(|node| node.id == "n1")
                .and_then(|node| node.position),
            Some([0, 0])
        );
    }

    #[test]
    fn a_node_is_sent_with_what_it_does_beside_its_fields_in_camel_case() {
        let drawn = graphs(&harvest());
        let node = serde_json::to_value(&drawn.graphs[0].nodes[2]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(node["does"], "kind");
        assert_eq!(node["kind"], "harvest_until");
        assert_eq!(
            (node["family"].clone(), node["fromStep"].clone()),
            ("action".into(), 5.into())
        );
        assert_eq!(node["pins"][0]["side"], "in");
    }

    #[test]
    fn the_editor_has_each_events_settings_each_functions_signature_and_the_problems_where_they_are() {
        let mut blueprint = bot::read(bot::HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let drawn = graphs(&blueprint);
        assert_eq!(drawn.problems, []);
        let life = drawn.graphs[0]
            .nodes
            .iter()
            .find(|node| node.id == "n5")
            .and_then(|node| node.event);
        assert_eq!(
            life,
            Some(EventView {
                priority: 2,
                mode: EventMode::InterruptThenResume
            })
        );
        let bank = &drawn.graphs[1].inputs;
        assert_eq!(
            bank.iter()
                .map(|it| (it.id.as_str(), it.value_type))
                .collect::<Vec<_>>(),
            [("bank", PinCarries::Bank)]
        );
        assert!(drawn.graphs[0].inputs.is_empty() && drawn.graphs[1].outputs.is_empty());
        blueprint.graphs[0].nodes[2]
            .inputs
            .insert(bot::Id("what".into()), serde_json::json!("some"));
        blueprint.graphs[0].nodes[1].does = Does::Get(bot::Id("bank".into()));
        let problems = graphs(&blueprint).problems;
        let at = |kind| problems.iter().find(|problem| problem.kind == kind).cloned();
        let typed = at(ProblemKind::ValueNotOfType).map(|it| (it.graph, it.node, it.pin));
        assert_eq!(
            typed,
            Some((Some("events".into()), Some("n3".into()), Some("what".into())))
        );
        let missing = at(ProblemKind::NoSuchVariable).map(|it| (it.node, it.subject));
        assert_eq!(missing, Some((Some("n2".into()), Some("bank".into()))));
    }

    #[test]
    fn every_problem_says_its_kind_and_its_place() {
        let id = |text: &str| bot::Id(text.to_owned());
        let end = |node: &str, pin: &str| bot::End(id(node), id(pin));
        let wire = Problem::WireOfAnotherType {
            graph: id("g"),
            from: end("a", "value"),
            to: end("b", "zone"),
        };
        let view = ProblemView::from(&wire);
        assert_eq!(
            (
                view.kind,
                view.node.as_deref(),
                view.pin.as_deref(),
                view.subject.as_deref()
            ),
            (ProblemKind::WireOfAnotherType, Some("a"), Some("value"), Some("b"))
        );
        let twice = ProblemView::from(&Problem::VariableTwice { variable: id("pods") });
        assert_eq!((twice.graph.as_deref(), twice.subject.as_deref()), (None, Some("pods")));
        let value = serde_json::to_value(&twice).unwrap_or_default();
        assert_eq!(value["kind"], "variableTwice");
    }
}
