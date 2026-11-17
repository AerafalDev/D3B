//! A blueprint's graphs as the editor draws them (`docs/bot.md`, "In the app"): each node with what it does, its family
//! and the step that makes it run, its pins from the catalogue, the values typed on them and where it sits; and the
//! wires, and what the picker may add to each graph. The editor's changes go through `edit_blueprint` (`ipc::edit`).

use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};
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
/// and pin, and the id it names as `subject`: the variable, function, kind, parameter or malformed id; for a wire to no
/// node, that node; for a wire twice or of another type, the node it goes to, its source being `node` (and `pin`).
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
    DefaultRangesOverlap,
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
    RangesOverlap,
    NoSuchOption,
    OptionMissing,
    CaseNotOfType,
    CaseTwice,
    TypeNotTaken,
    WildcardWithoutType,
    WireToNoPin,
    WireTheWrongWay,
    WireOfAnotherType,
    WireOfAnotherShape,
    OutputWiredTwice,
    InputFedTwice,
    InputWithoutValue,
    EventTwice,
    OwnEventTwice,
    NoSuchEvent,
    NoSuchExit,
    NotPure,
    LeaveOutsideLoop,
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
            Problem::DefaultRangesOverlap { variable } => (K::DefaultRangesOverlap, None, None, None, text(variable)),
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
            Problem::RangesOverlap { graph, node, pin } => (K::RangesOverlap, text(graph), text(node), text(pin), None),
            Problem::NoSuchOption { graph, node, option } => {
                (K::NoSuchOption, text(graph), text(node), None, text(option))
            }
            Problem::OptionMissing { graph, node, option } => {
                (K::OptionMissing, text(graph), text(node), None, text(option))
            }
            Problem::CaseNotOfType { graph, node, pin } => (K::CaseNotOfType, text(graph), text(node), text(pin), None),
            Problem::CaseTwice { graph, node, pin } => (K::CaseTwice, text(graph), text(node), text(pin), None),
            Problem::TypeNotTaken { graph, node } => (K::TypeNotTaken, text(graph), text(node), None, None),
            Problem::WildcardWithoutType { graph, node } => {
                (K::WildcardWithoutType, text(graph), text(node), None, None)
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
            Problem::WireOfAnotherShape { graph, from, to } => (
                K::WireOfAnotherShape,
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
            Problem::OwnEventTwice { event } => (K::OwnEventTwice, None, None, None, text(event)),
            Problem::NoSuchEvent { graph, node, event } => (K::NoSuchEvent, text(graph), text(node), None, text(event)),
            Problem::NoSuchExit { graph, node, exit } => (K::NoSuchExit, text(graph), text(node), None, text(exit)),
            Problem::NotPure { graph, node } => (K::NotPure, text(graph), text(node), None, None),
            Problem::LeaveOutsideLoop { graph, node } => (K::LeaveOutsideLoop, text(graph), text(node), None, None),
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

impl From<Mode> for EventMode {
    fn from(mode: Mode) -> Self {
        match mode {
            Mode::InterruptThenResume => Self::InterruptThenResume,
            Mode::WaitForAction => Self::WaitForAction,
            Mode::Restart => Self::Restart,
            Mode::Parallel => Self::Parallel,
        }
    }
}

/// One of a function's inputs or outputs.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct ParameterView {
    pub id: String,
    pub name: String,
    pub value_type: PinCarries,
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub list: bool,
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub ranges: bool,
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
    /// What the picker may add to it (`bot::choices`), with the pins a wire dragged is checked against.
    pub choices: Vec<ChoiceView>,
}

/// A node the picker may add: what it does, its family, the step from which it runs, and its pins.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct ChoiceView {
    #[serde(flatten)]
    pub does: NodeDoes,
    pub family: Option<NodeFamily>,
    pub from_step: Option<u8>,
    pub pins: Vec<PinView>,
    /// The types its wildcards may take, the first while its node notes none; omitted for any type.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    #[specta(optional)]
    pub types: Vec<String>,
}

/// What a node holds beyond its inputs (`bot::Options`): the type its wildcards take, the pins its options make and what
/// of the blueprint it names; each omitted when not given.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct OptionsView {
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub value_type: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub count: Option<u8>,
    /// Switch's cases, each as JSON.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    #[specta(optional)]
    pub cases: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub text: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub expression: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub event: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub variable: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub exit: Option<String>,
}

impl OptionsView {
    fn is_empty(&self) -> bool {
        self == &Self::default()
    }
}

impl From<&bot::Options> for OptionsView {
    fn from(options: &bot::Options) -> Self {
        let id = |id: &Option<bot::Id>| id.as_ref().map(|it| it.0.clone());
        Self {
            value_type: options.value_type.map(type_name),
            count: options.count.map(std::num::NonZeroU8::get),
            cases: options.cases.iter().map(ToString::to_string).collect(),
            text: options.text.clone(),
            expression: options.expression.clone(),
            event: id(&options.event),
            variable: id(&options.variable),
            exit: id(&options.exit),
        }
    }
}

/// A type as the file writes it: `zone`, `duration`…
fn type_name(value_type: ValueType) -> String {
    serde_json::to_value(value_type)
        .ok()
        .and_then(|value| value.as_str().map(str::to_owned))
        .unwrap_or_default()
}

/// The types `does`'s kind lets its wildcards take, by name.
fn kind_types(does: &Does) -> Vec<String> {
    let Does::Kind(kind) = does else {
        return Vec::new();
    };
    bot::kind(&kind.0).map_or_else(Vec::new, |kind| kind.types.iter().copied().map(type_name).collect())
}

/// What a node does.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(tag = "does", rename_all = "camelCase")]
pub enum NodeDoes {
    /// One of the catalogue's kinds, by id; its sentence is the interface's `kinds.<kind>` of the nodes' words.
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
    /// What it holds beyond its inputs; omitted when nothing.
    #[serde(skip_serializing_if = "OptionsView::is_empty")]
    #[specta(optional)]
    pub options: OptionsView,
    /// The types its wildcards may take, the first while it notes none; omitted for any type.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    #[specta(optional)]
    pub types: Vec<String>,
    /// The pins "+" adds, Sequence's outputs; omitted for a kind that numbers none.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[specta(optional)]
    pub adds: Option<AddsView>,
    /// Whether it takes cases, each a flow output, typed in Details: Switch; omitted for no.
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub takes_cases: bool,
    /// A call's named exits, each its flow output's id and the name the player gave it; omitted for none.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    #[specta(optional)]
    pub exits: Vec<ExitView>,
}

/// A function's named exit as its call draws it: its pin's id, and its name.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct ExitView {
    pub id: String,
    pub name: String,
}

/// The named exits of the function `does` calls, in its signature's order.
fn exits(blueprint: &Blueprint, does: &Does) -> Vec<ExitView> {
    let Does::Call(function) = does else {
        return Vec::new();
    };
    let signature = blueprint
        .graphs
        .iter()
        .find(|graph| &graph.id == function)
        .and_then(|graph| graph.function.as_ref());
    let exits = signature.into_iter().flat_map(|signature| &signature.exits);
    exits
        .map(|exit| ExitView {
            id: exit.id.0.clone(),
            name: exit.name.clone(),
        })
        .collect()
}

/// The numbered pins a node's "+" adds: `<pin>_1` to `<pin>_<count>`, at least `least` of them.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct AddsView {
    pub pin: String,
    pub side: PinSide,
    pub least: u8,
}

/// The pins `does`'s kind numbers, if any.
fn adds(does: &Does) -> Option<AddsView> {
    let Does::Kind(kind) = does else {
        return None;
    };
    bot::kind(&kind.0)?.takes.iter().find_map(|takes| match *takes {
        bot::Takes::Count { pin, least } => Some(AddsView {
            pin: pin.id.to_owned(),
            side: if pin.side == bot::Side::In {
                PinSide::In
            } else {
                PinSide::Out
            },
            least,
        }),
        _ => None,
    })
}

/// A pin of a node.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct PinView {
    pub id: String,
    pub side: PinSide,
    pub carries: PinCarries,
    /// A list of values (a square pin), rather than one; omitted for no.
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub list: bool,
    /// One of its kind's wildcards, typed or not: the editor checks a wire into it by its kind's types.
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub wildcard: bool,
    /// Ranges whose rows hold a value, or a list with `list`.
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    #[specta(optional)]
    pub ranges: bool,
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
    /// A value computed from its inputs, drawn compact.
    Value,
    Function,
    Variable,
    Call,
}

/// Which way a pin goes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum PinSide {
    In,
    Out,
}

/// What a pin carries: the flow, a value of a type, or a wildcard whose node notes no type yet.
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
    Map,
    Item,
    Duration,
    Time,
    Npc,
    Wildcard,
    /// Any value: a Text's hole.
    Any,
}

impl From<Carries> for PinCarries {
    fn from(carries: Carries) -> Self {
        match carries {
            Carries::Exec => Self::Exec,
            Carries::Wildcard(_) => Self::Wildcard,
            Carries::Any => Self::Any,
            Carries::Value(typed) => match typed.of {
                ValueType::Bool => Self::Bool,
                ValueType::Number => Self::Number,
                ValueType::Percentage => Self::Percentage,
                ValueType::Text => Self::Text,
                ValueType::Zone => Self::Zone,
                ValueType::Route => Self::Route,
                ValueType::Bank => Self::Bank,
                ValueType::Items => Self::Items,
                ValueType::Job => Self::Job,
                ValueType::Map => Self::Map,
                ValueType::Item => Self::Item,
                ValueType::Duration => Self::Duration,
                ValueType::Time => Self::Time,
                ValueType::Npc => Self::Npc,
            },
        }
    }
}

/// Whether what a pin carries is a list, and ranges.
fn shape(carries: Carries) -> (bool, bool) {
    match carries {
        Carries::Exec | Carries::Any => (false, false),
        Carries::Value(bot::Type { shape, .. }) | Carries::Wildcard(shape) => (shape.list, shape.ranges),
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
                    Family::Value => NodeFamily::Value,
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

impl From<&Does> for NodeDoes {
    fn from(does: &Does) -> Self {
        match does {
            Does::Kind(kind) => Self::Kind { kind: kind.0.clone() },
            Does::Get(variable) => Self::Get {
                variable: variable.0.clone(),
            },
            Does::Set(variable) => Self::Set {
                variable: variable.0.clone(),
            },
            Does::Call(function) => Self::Call {
                function: function.0.clone(),
            },
        }
    }
}

impl From<NodeDoes> for Does {
    fn from(does: NodeDoes) -> Self {
        match does {
            NodeDoes::Kind { kind } => Self::Kind(bot::Id(kind)),
            NodeDoes::Get { variable } => Self::Get(bot::Id(variable)),
            NodeDoes::Set { variable } => Self::Set(bot::Id(variable)),
            NodeDoes::Call { function } => Self::Call(bot::Id(function)),
        }
    }
}

fn pin_views(pins: Vec<bot::NodePin>, does: &Does) -> Vec<PinView> {
    let kind = match does {
        Does::Kind(kind) => bot::kind(&kind.0),
        _ => None,
    };
    let views = pins.into_iter().map(|pin| {
        let (list, ranges) = shape(pin.carries);
        let wildcard = kind.is_some_and(|kind| {
            kind.pins
                .iter()
                .any(|it| it.id == pin.id && it.side == pin.side && matches!(it.carries, Carries::Wildcard(_)))
        });
        PinView {
            wildcard,
            id: pin.id,
            side: if pin.side == Side::In {
                PinSide::In
            } else {
                PinSide::Out
            },
            carries: pin.carries.into(),
            list,
            ranges,
            default: pin.default.map(|value| value.to_string()),
        }
    });
    views.collect()
}

/// What the picker may add to `graph`, each as a node not placed yet.
fn choices(blueprint: &Blueprint, graph: &bot::Graph) -> Vec<ChoiceView> {
    let choices = bot::choices(blueprint, graph).into_iter().map(|does| {
        let node = bot::Node {
            id: bot::Id(String::new()),
            does,
            inputs: BTreeMap::new(),
            event: None,
            options: bot::Options::default(),
        };
        let (family, from_step) = family(&node);
        ChoiceView {
            does: (&node.does).into(),
            family,
            from_step,
            pins: pin_views(bot::pins(blueprint, graph, &node).unwrap_or_default(), &node.does),
            types: kind_types(&node.does),
        }
    });
    choices.collect()
}

fn parameters(parameters: Option<&Vec<bot::Parameter>>) -> Vec<ParameterView> {
    let views = parameters.into_iter().flatten().map(|parameter| ParameterView {
        id: parameter.id.0.clone(),
        name: parameter.name.clone(),
        value_type: Carries::one(parameter.value_type).into(),
        list: parameter.list,
        ranges: parameter.ranges,
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
                does: (&node.does).into(),
                family,
                from_step,
                pins: pin_views(pins, &node.does),
                options: (&node.options).into(),
                types: kind_types(&node.does),
                adds: adds(&node.does),
                takes_cases: matches!(&node.does, Does::Kind(kind)
                    if bot::kind(&kind.0).is_some_and(|kind| kind.takes.contains(&bot::Takes::Cases))),
                exits: exits(blueprint, &node.does),
                inputs: node
                    .inputs
                    .iter()
                    .map(|(pin, value)| (pin.0.clone(), value.to_string()))
                    .collect(),
                position: layout.and_then(|nodes| nodes.get(&node.id)).copied(),
                event: node.event.map(|event| EventView {
                    priority: event.priority.get(),
                    mode: event.mode.into(),
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
            choices: choices(blueprint, graph),
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
    fn a_comparison_is_drawn_with_the_types_it_takes_its_wildcards_marked_and_its_options() {
        let mut blueprint = harvest();
        let ge = r#"{"id": "ge", "kind": "greater_or_equal", "options": {"type": "time"}}"#;
        blueprint.graphs[0]
            .nodes
            .push(serde_json::from_str(ge).unwrap_or_else(|error| panic!("{error}")));
        let drawn = graphs(&blueprint);
        let ge = drawn.graphs[0].nodes.iter().find(|it| it.id == "ge");
        let ge = ge.unwrap_or_else(|| panic!("ge"));
        assert_eq!(ge.types, ["number", "percentage", "duration", "time"]);
        assert_eq!(ge.options.value_type.as_deref(), Some("time"));
        let marked: Vec<_> = ge
            .pins
            .iter()
            .map(|pin| (pin.id.as_str(), pin.wildcard, pin.carries))
            .collect();
        assert_eq!(
            marked,
            [
                ("a", true, PinCarries::Time),
                ("b", true, PinCarries::Time),
                ("value", false, PinCarries::Bool)
            ]
        );
        let choice = drawn.graphs[0].choices.iter().find(|it| {
            it.does
                == NodeDoes::Kind {
                    kind: "greater_or_equal".into(),
                }
        });
        assert_eq!(choice.map(|it| it.types.len()), Some(4));
        let serialized = serde_json::to_string(&drawn.graphs[0].nodes[0]).unwrap_or_default();
        assert!(!serialized.contains("options") && !serialized.contains("types") && !serialized.contains("wildcard"));
    }

    #[test]
    fn a_pin_is_drawn_with_its_shape_and_a_wildcard_as_one_until_its_node_notes_a_type() {
        let mut blueprint = harvest();
        let zone = blueprint.variables.iter_mut().find(|it| it.id.0 == "zone");
        if let Some(zone) = zone {
            zone.list = true;
        }
        let each = r#"{"id": "each", "kind": "for_each"}"#;
        blueprint.graphs[0]
            .nodes
            .push(serde_json::from_str(each).unwrap_or_else(|error| panic!("{error}")));
        let pin = |blueprint: &Blueprint, node: &str, pin: &str| {
            let drawn = graphs(blueprint);
            let node = drawn.graphs[0].nodes.iter().find(|it| it.id == node).cloned();
            let pin = node.and_then(|node| node.pins.into_iter().find(|it| it.id == pin));
            pin.map(|it| (it.carries, it.list, it.ranges))
        };
        assert_eq!(pin(&blueprint, "zone", "value"), Some((PinCarries::Zone, true, false)));
        assert_eq!(
            pin(&blueprint, "each", "list"),
            Some((PinCarries::Wildcard, true, false))
        );
        if let Some(each) = blueprint.graphs[0].nodes.last_mut() {
            each.options.value_type = Some(ValueType::Npc);
        }
        assert_eq!(
            pin(&blueprint, "each", "element"),
            Some((PinCarries::Npc, false, false))
        );
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
    fn a_call_is_drawn_with_its_functions_exits_by_name_and_another_node_with_none() {
        let text = r#"{"schema": 2, "id": "b", "name": "B", "version": 1,
          "graphs": [{"id": "events", "name": "Events", "nodes": [{"id": "c", "call": "f"}, {"id": "n", "kind": "notify"}]},
            {"id": "f", "name": "F", "function": {"exits": [{"id": "full", "name": "Plein"}]}, "nodes": []}]}"#;
        let drawn = graphs(&bot::read(text).unwrap_or_else(|error| panic!("{error}")));
        let exits = |node: usize| drawn.graphs[0].nodes[node].exits.clone();
        let full = ExitView {
            id: "full".into(),
            name: "Plein".into(),
        };
        assert_eq!((exits(0), exits(1)), (vec![full], Vec::new()));
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

    #[test]
    #[expect(clippy::too_many_lines, reason = "one line per variant of `bot::Problem`, a table")]
    fn every_problem_goes_to_its_kind_and_its_place() {
        use ProblemKind as K;
        let id = |text: &str| bot::Id(text.to_owned());
        let (g, n, p, x) = (|| id("g"), || id("n"), || id("p"), || id("x"));
        let some = |text: &str| Some(text.to_owned());
        let cases = [
            (
                Problem::MalformedId { id: x() },
                (K::MalformedId, None, None, None, some("x")),
            ),
            (
                Problem::VariableTwice { variable: x() },
                (K::VariableTwice, None, None, None, some("x")),
            ),
            (
                Problem::GraphTwice { graph: g() },
                (K::GraphTwice, some("g"), None, None, None),
            ),
            (
                Problem::NodeTwice { graph: g(), node: n() },
                (K::NodeTwice, some("g"), some("n"), None, None),
            ),
            (
                Problem::ParameterTwice {
                    graph: g(),
                    parameter: x(),
                },
                (K::ParameterTwice, some("g"), None, None, some("x")),
            ),
            (
                Problem::DefaultNotOfType { variable: x() },
                (K::DefaultNotOfType, None, None, None, some("x")),
            ),
            (
                Problem::WrongBounds { variable: x() },
                (K::WrongBounds, None, None, None, some("x")),
            ),
            (
                Problem::DefaultOutOfBounds { variable: x() },
                (K::DefaultOutOfBounds, None, None, None, some("x")),
            ),
            (
                Problem::NoSuchVariable {
                    graph: g(),
                    node: n(),
                    variable: x(),
                },
                (K::NoSuchVariable, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::NoSuchFunction {
                    graph: g(),
                    node: n(),
                    function: x(),
                },
                (K::NoSuchFunction, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::WireToNoNode { graph: g(), node: x() },
                (K::WireToNoNode, some("g"), None, None, some("x")),
            ),
            (
                Problem::WireTwice {
                    graph: g(),
                    from: n(),
                    to: x(),
                },
                (K::WireTwice, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::UnknownKind {
                    graph: g(),
                    node: n(),
                    kind: x(),
                },
                (K::UnknownKind, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::OutOfPlace { graph: g(), node: n() },
                (K::OutOfPlace, some("g"), some("n"), None, None),
            ),
            (
                Problem::EventSettingsOnNoEvent { graph: g(), node: n() },
                (K::EventSettingsOnNoEvent, some("g"), some("n"), None, None),
            ),
            (
                Problem::NoSuchInput {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::NoSuchInput, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::ValueNotOfType {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::ValueNotOfType, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::WireToNoPin {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::WireToNoPin, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::WireTheWrongWay {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::WireTheWrongWay, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::WireOfAnotherType {
                    graph: g(),
                    from: bot::End(n(), p()),
                    to: bot::End(x(), id("q")),
                },
                (K::WireOfAnotherType, some("g"), some("n"), some("p"), some("x")),
            ),
            (
                Problem::OutputWiredTwice {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::OutputWiredTwice, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::InputFedTwice {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::InputFedTwice, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::InputWithoutValue {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::InputWithoutValue, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::EventTwice { graph: g(), node: n() },
                (K::EventTwice, some("g"), some("n"), None, None),
            ),
            (
                Problem::OwnEventTwice { event: x() },
                (K::OwnEventTwice, None, None, None, some("x")),
            ),
            (
                Problem::NoSuchEvent {
                    graph: g(),
                    node: n(),
                    event: x(),
                },
                (K::NoSuchEvent, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::NoSuchExit {
                    graph: g(),
                    node: n(),
                    exit: x(),
                },
                (K::NoSuchExit, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::NotPure { graph: g(), node: n() },
                (K::NotPure, some("g"), some("n"), None, None),
            ),
            (
                Problem::LeaveOutsideLoop { graph: g(), node: n() },
                (K::LeaveOutsideLoop, some("g"), some("n"), None, None),
            ),
            (
                Problem::DefaultRangesOverlap { variable: x() },
                (K::DefaultRangesOverlap, None, None, None, some("x")),
            ),
            (
                Problem::RangesOverlap {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::RangesOverlap, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::NoSuchOption {
                    graph: g(),
                    node: n(),
                    option: x(),
                },
                (K::NoSuchOption, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::OptionMissing {
                    graph: g(),
                    node: n(),
                    option: x(),
                },
                (K::OptionMissing, some("g"), some("n"), None, some("x")),
            ),
            (
                Problem::CaseNotOfType {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::CaseNotOfType, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::CaseTwice {
                    graph: g(),
                    node: n(),
                    pin: p(),
                },
                (K::CaseTwice, some("g"), some("n"), some("p"), None),
            ),
            (
                Problem::TypeNotTaken { graph: g(), node: n() },
                (K::TypeNotTaken, some("g"), some("n"), None, None),
            ),
            (
                Problem::WildcardWithoutType { graph: g(), node: n() },
                (K::WildcardWithoutType, some("g"), some("n"), None, None),
            ),
            (
                Problem::WireOfAnotherShape {
                    graph: g(),
                    from: bot::End(n(), p()),
                    to: bot::End(x(), p()),
                },
                (K::WireOfAnotherShape, some("g"), some("n"), some("p"), some("x")),
            ),
        ];
        let mut kinds = std::collections::HashSet::new();
        for (problem, expected) in cases {
            let view = ProblemView::from(&problem);
            assert_eq!(
                (view.kind, view.graph, view.node, view.pin, view.subject),
                expected,
                "{problem:?}"
            );
            assert!(
                kinds.insert(serde_json::to_string(&view.kind).unwrap_or_default()),
                "{problem:?}: a kind's name twice"
            );
        }
        assert_eq!(kinds.len(), 38, "every problem");
    }

    #[test]
    fn every_mode_goes_to_its_own() {
        let modes = [
            Mode::InterruptThenResume,
            Mode::WaitForAction,
            Mode::Restart,
            Mode::Parallel,
        ]
        .map(EventMode::from);
        assert_eq!(
            modes,
            [
                EventMode::InterruptThenResume,
                EventMode::WaitForAction,
                EventMode::Restart,
                EventMode::Parallel
            ]
        );
    }

    #[test]
    fn each_graph_gives_the_picker_its_choices_with_their_family_step_and_pins() {
        let blueprint = bot::read(bot::HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let drawn = graphs(&blueprint);
        let choice = |graph: usize, does: &NodeDoes| drawn.graphs[graph].choices.iter().find(|it| &it.does == does);
        let harvest = choice(
            0,
            &NodeDoes::Kind {
                kind: "harvest_until".into(),
            },
        );
        assert_eq!(
            harvest.map(|it| (it.family, it.from_step, it.pins.len())),
            Some((Some(NodeFamily::Action), Some(5), 8))
        );
        let set = choice(
            0,
            &NodeDoes::Set {
                variable: "pods".into(),
            },
        );
        assert_eq!(
            set.map(|it| it.pins.iter().map(|pin| (pin.side, pin.carries)).collect::<Vec<_>>()),
            Some(vec![
                (PinSide::Out, PinCarries::Percentage),
                (PinSide::In, PinCarries::Exec),
                (PinSide::Out, PinCarries::Exec),
                (PinSide::In, PinCarries::Percentage)
            ])
        );
        assert!(
            choice(
                0,
                &NodeDoes::Kind {
                    kind: "on_start".into()
                }
            )
            .is_none(),
            "held once"
        );
        let entry = choice(1, &NodeDoes::Kind { kind: "entry".into() });
        assert_eq!(
            entry.map(|it| it.pins.len()),
            Some(2),
            "its flow and the function's input"
        );
    }
}
