//! The editor's changes to a blueprint of mine (`docs/bot.md`, "In the app"; `bot::edit`), each saved as it is made
//! (the owner, 2026-11-16): the blueprint's graphs given back, with their problems, as `blueprint_graphs` gives them.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use super::blueprints::PlayersFiles;
use super::graph::{BlueprintGraphs, NodeDoes, PinSide, graphs};
use super::played::PlayedChanged;
use super::played::follow::follow;
use crate::bot::{self, End, Id, Refusal, Side};
use crate::storage::{StorageError, Store};

/// One change to a graph.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
#[serde(tag = "edit", rename_all = "camelCase")]
pub enum GraphEdit {
    /// A wire from an output to an input, `[node, pin]` each, in place of the one that fed the input or left the flow
    /// output.
    Wire { from: [String; 2], to: [String; 2] },
    /// A node the picker offers, at `position`, wired to the pin a wire dropped on the ground came from.
    #[serde(rename_all = "camelCase")]
    Add {
        node: NodeDoes,
        position: [i32; 2],
        wired_to: Option<DraggedPin>,
    },
    /// Nodes put elsewhere, dragged together: one change, undone at once.
    Move { nodes: Vec<MovedNode> },
    /// Nodes, with every wire to or from them, and wires, chosen together.
    Delete { nodes: Vec<String>, wires: Vec<WireEnds> },
    /// A value typed on the input `pin` of `node`, as JSON, which no wire feeds; none, or the pin's default, drops it.
    Value {
        node: String,
        pin: String,
        value: Option<String>,
    },
    /// A variable of mine named so, yes or no, under an id made from its name.
    NewVariable { name: String },
    /// The variable of its id made this one, whole; a new type drops what it no longer fits.
    Variable { variable: VariableChange },
    /// The variable `variable` gone, with the nodes that read or write it.
    DeleteVariable { variable: String },
    /// The blueprint named and described so, its id and file kept.
    About { name: String, description: String },
}

/// A variable of mine as the editor changes it, whole.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
#[expect(
    clippy::struct_excessive_bools,
    reason = "a variable's list, ranges, setting and memory, each the player's yes or no, independent of the others"
)]
#[serde(rename_all = "camelCase")]
pub struct VariableChange {
    pub id: String,
    pub name: String,
    /// Its type, as the file writes it: `zone`, `percentage`, `bank`…
    pub value_type: String,
    /// A list of values of its type.
    pub list: bool,
    /// Ranges of values of its type, or of lists with `list`.
    pub ranges: bool,
    /// Its default as JSON; none for no default, which each character then sets.
    pub default: Option<String>,
    /// The smallest and largest value of a number or a percentage.
    pub bounds: Option<[i32; 2]>,
    pub per_character: bool,
    pub memory: bool,
    pub help: String,
}

impl TryFrom<VariableChange> for bot::Variable {
    type Error = EditError;

    fn try_from(change: VariableChange) -> Result<Self, EditError> {
        let value_type =
            serde_json::from_value(serde_json::Value::String(change.value_type)).map_err(|_| EditError::NotItsType)?;
        let default = change
            .default
            .map(|json| serde_json::from_str(&json))
            .transpose()
            .map_err(|_| EditError::NotItsType)?
            .unwrap_or_default();
        Ok(Self {
            id: Id(change.id),
            name: change.name,
            value_type,
            list: change.list,
            ranges: change.ranges,
            default,
            bounds: change
                .bounds
                .map(|[smallest, largest]| [smallest.into(), largest.into()]),
            per_character: change.per_character,
            memory: change.memory,
            help: change.help,
        })
    }
}

/// A node and where it was put.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
pub struct MovedNode {
    pub node: String,
    pub position: [i32; 2],
}

/// A wire by its ends, `[node, pin]` each.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
pub struct WireEnds {
    pub from: [String; 2],
    pub to: [String; 2],
}

/// The pin a wire was dragged from: an input's, or an output's.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
pub struct DraggedPin {
    pub node: String,
    pub pin: String,
    pub side: PinSide,
}

/// Why a blueprint was not changed. The editor offers no refused edit: one says the file changed under it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum EditError {
    NoFolder,
    /// None of mine has this id: its file is gone.
    NoSuchBlueprint,
    /// One of D3B's, which only Duplicate changes.
    Locked,
    NoSuchGraph,
    NoSuchNode,
    NoSuchWire,
    NoSuchPin,
    WrongWay,
    OtherType,
    SameNode,
    NotAChoice,
    /// A wire feeds the input a value was typed on.
    Wired,
    /// Not JSON, not of the input's type, or out of its bounds.
    NotItsType,
    /// The blueprint has no variable of this id.
    NoSuchVariable,
    /// A name of nothing but spaces.
    Blank,
    /// The system could not read or write its file, or it no longer reads and is kept as it is: the reason, in words.
    NotSaved {
        reason: String,
    },
}

impl From<StorageError> for EditError {
    fn from(error: StorageError) -> Self {
        match error {
            StorageError::NoSuchBlueprint { .. } => Self::NoSuchBlueprint,
            StorageError::GivenId { .. } => Self::Locked,
            error => Self::NotSaved {
                reason: error.to_string(),
            },
        }
    }
}

impl From<Refusal> for EditError {
    fn from(refusal: Refusal) -> Self {
        match refusal {
            Refusal::NoSuchGraph => Self::NoSuchGraph,
            Refusal::NoSuchNode => Self::NoSuchNode,
            Refusal::NoSuchWire => Self::NoSuchWire,
            Refusal::NoSuchPin => Self::NoSuchPin,
            Refusal::WrongWay => Self::WrongWay,
            Refusal::OtherType => Self::OtherType,
            Refusal::SameNode => Self::SameNode,
            Refusal::NotAChoice => Self::NotAChoice,
            Refusal::Wired => Self::Wired,
            Refusal::NotItsType => Self::NotItsType,
            Refusal::NoSuchVariable => Self::NoSuchVariable,
            Refusal::Blank => Self::Blank,
        }
    }
}

fn end([node, pin]: [String; 2]) -> End {
    End(Id(node), Id(pin))
}

impl TryFrom<GraphEdit> for bot::Edit {
    type Error = EditError;

    fn try_from(edit: GraphEdit) -> Result<Self, EditError> {
        Ok(match edit {
            GraphEdit::Wire { from, to } => Self::Wire {
                from: end(from),
                to: end(to),
            },
            GraphEdit::Add {
                node,
                position,
                wired_to,
            } => Self::Add {
                does: node.into(),
                position,
                wired_to: wired_to.map(|pin| {
                    let side = if pin.side == PinSide::In { Side::In } else { Side::Out };
                    (End(Id(pin.node), Id(pin.pin)), side)
                }),
            },
            GraphEdit::Move { nodes } => Self::Move {
                nodes: nodes.into_iter().map(|it| (Id(it.node), it.position)).collect(),
            },
            GraphEdit::Delete { nodes, wires } => Self::Delete {
                nodes: nodes.into_iter().map(Id).collect(),
                wires: wires
                    .into_iter()
                    .map(|it| bot::Wire {
                        from: end(it.from),
                        to: end(it.to),
                    })
                    .collect(),
            },
            GraphEdit::Value { node, pin, value } => Self::Value {
                node: Id(node),
                pin: Id(pin),
                value: value
                    .map(|json| serde_json::from_str(&json))
                    .transpose()
                    .map_err(|_| EditError::NotItsType)?,
            },
            GraphEdit::NewVariable { name } => Self::NewVariable { name },
            GraphEdit::Variable { variable } => Self::Variable(variable.try_into()?),
            GraphEdit::DeleteVariable { variable } => Self::DeleteVariable { id: Id(variable) },
            GraphEdit::About { name, description } => Self::About { name, description },
        })
    }
}

/// Makes `edit` in the graph `graph` of my blueprint `id` and saves it, the characters running it carried to its
/// variables; its graphs then, and whether a character's settings changed.
fn change(store: &Store, id: &str, graph: &str, edit: GraphEdit) -> Result<(BlueprintGraphs, bool), EditError> {
    let graph = Id(graph.to_owned());
    let edit = bot::Edit::try_from(edit)?;
    // Read under the lock the change takes, so the settings are carried from the variables it changed.
    let mut before = Vec::new();
    let blueprint = store.change_blueprint(id, |blueprint| {
        before.clone_from(&blueprint.variables);
        bot::edit(blueprint, &graph, edit).map_err(EditError::from)
    })?;
    let carried = follow(store, id, &before, &blueprint.variables);
    Ok((graphs(&blueprint), carried))
}

/// Undo, or redo, of the last change to my blueprint `id`, written at once, the characters running it carried to its
/// variables; its graphs then, and whether a character's settings changed.
fn step(store: &Store, id: &str, back: bool) -> Result<(BlueprintGraphs, bool), EditError> {
    // Undo writes under its own lock: the variables are read before it, the editor sending one change at a time. A
    // file that does not read carries nothing, rather than dropping every setting.
    let before = store.read_blueprint(id).ok().map(|it| it.variables);
    let blueprint = if back {
        store.undo_blueprint(id)
    } else {
        store.redo_blueprint(id)
    }?;
    let carried = before.is_some_and(|before| follow(store, id, &before, &blueprint.variables));
    Ok((graphs(&blueprint), carried))
}

/// Runs `change` on the player's folder away from the webview's thread; the characters' tabs hear when their settings
/// followed.
async fn saved(
    app: tauri::AppHandle,
    change: impl FnOnce(&Store) -> Result<(BlueprintGraphs, bool), EditError> + Send + 'static,
) -> Result<BlueprintGraphs, EditError> {
    let handle = app.clone();
    let done = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        change(files.store().ok_or(EditError::NoFolder)?)
    });
    let (drawn, carried) = done.await.unwrap_or_else(|error| {
        Err(EditError::NotSaved {
            reason: error.to_string(),
        })
    })?;
    if carried {
        let _ = PlayedChanged.emit(&handle);
    }
    Ok(drawn)
}

/// Makes `edit` in the graph `graph` of my blueprint `id`, saved at once; the blueprint's graphs and problems then. A
/// variable's edit is the blueprint's, whichever graph shows.
#[tauri::command]
#[specta::specta]
pub async fn edit_blueprint(
    app: tauri::AppHandle,
    id: String,
    graph: String,
    edit: GraphEdit,
) -> Result<BlueprintGraphs, EditError> {
    saved(app, move |store| change(store, &id, &graph, edit)).await
}

/// Undo: my blueprint `id` as it was before its last change, in any of its graphs, saved at once (the owner,
/// 2026-11-16); its graphs and problems then, as they are when nothing is left to undo.
#[tauri::command]
#[specta::specta]
pub async fn undo_blueprint(app: tauri::AppHandle, id: String) -> Result<BlueprintGraphs, EditError> {
    saved(app, move |store| step(store, &id, true)).await
}

/// Redo: my blueprint `id` as it was before its last undo, saved at once; its graphs and problems then.
#[tauri::command]
#[specta::specta]
pub async fn redo_blueprint(app: tauri::AppHandle, id: String) -> Result<BlueprintGraphs, EditError> {
    saved(app, move |store| step(store, &id, false)).await
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn mine(name: &str) -> (Folder, Store) {
        let folder = Folder::new(name);
        let store = Store::new(folder.path().to_path_buf());
        let text = bot::HARVEST.replacen("automatic-harvest", "my-harvest", 1);
        let blueprint = bot::read(&text).unwrap_or_else(|error| panic!("{error}"));
        store
            .save_blueprint(&blueprint)
            .unwrap_or_else(|error| panic!("{error}"));
        (folder, store)
    }

    fn changed(store: &Store, id: &str, graph: &str, edit: GraphEdit) -> Result<BlueprintGraphs, EditError> {
        change(store, id, graph, edit).map(|(drawn, _)| drawn)
    }

    fn stepped(store: &Store, id: &str, back: bool) -> Result<BlueprintGraphs, EditError> {
        step(store, id, back).map(|(drawn, _)| drawn)
    }

    fn parsed(json: &str) -> GraphEdit {
        serde_json::from_str(json).unwrap_or_else(|error| panic!("{error}"))
    }

    #[test]
    fn each_edit_comes_in_its_json_is_saved_and_gives_the_graphs_back() {
        let (_folder, store) = mine("ipc-edit-add");
        let add = parsed(
            r#"{"edit": "add", "node": {"does": "kind", "kind": "notify"}, "position": [400, 120],
                "wiredTo": {"node": "n3", "pin": "failed", "side": "out"}}"#,
        );
        let drawn = changed(&store, "my-harvest", "events", add).unwrap_or_else(|error| panic!("{error:?}"));
        let events = &drawn.graphs[0];
        let notify = events.nodes.iter().find(|node| node.id == "notify");
        assert_eq!(notify.and_then(|node| node.position), Some([400, 120]));
        let wire = ["n3".to_owned(), "failed".to_owned()];
        assert!(events.wires.iter().any(|it| it.from == wire && it.to[0] == "notify"));
        assert_eq!(store.blueprints().read[0].graphs[0].nodes.len(), 6, "written at once");
        let wire = parsed(r#"{"edit": "wire", "from": ["n3", "done"], "to": ["n4", "run"]}"#);
        assert!(changed(&store, "my-harvest", "events", wire).is_ok());
        let moved = parsed(r#"{"edit": "move", "nodes": [{"node": "n4", "position": [-5, 9]}]}"#);
        let drawn = changed(&store, "my-harvest", "events", moved).unwrap_or_else(|error| panic!("{error:?}"));
        let n4 = drawn.graphs[0].nodes.iter().find(|node| node.id == "n4");
        assert_eq!(
            (n4.and_then(|node| node.position), drawn.graphs[0].wires.len()),
            (Some([-5, 9]), 4)
        );
        let delete =
            parsed(r#"{"edit": "delete", "nodes": ["n4"], "wires": [{"from": ["n1", "then"], "to": ["n3", "run"]}]}"#);
        let drawn = changed(&store, "my-harvest", "events", delete).unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!((drawn.graphs[0].nodes.len(), drawn.graphs[0].wires.len()), (5, 2));
        let typed = parsed(r#"{"edit": "value", "node": "n3", "pin": "pods", "value": "80"}"#);
        let drawn = changed(&store, "my-harvest", "events", typed).unwrap_or_else(|error| panic!("{error:?}"));
        let n3 = drawn.graphs[0].nodes.iter().find(|node| node.id == "n3");
        assert_eq!(
            n3.and_then(|node| node.inputs.get("pods")).map(String::as_str),
            Some("80")
        );
    }

    #[test]
    fn a_variable_is_created_changed_and_deleted_from_its_json_whichever_graph_shows() {
        let (_folder, store) = mine("ipc-edit-variable");
        let variables = || store.blueprints().read[0].variables.clone();
        let new = parsed(r#"{"edit": "newVariable", "name": "Objectif"}"#);
        assert!(changed(&store, "my-harvest", "empty_at_bank", new).is_ok());
        assert_eq!(variables()[3].id.0, "objectif");
        let goal = parsed(
            r#"{"edit": "variable", "variable": {"id": "objectif", "name": "Objectif", "valueType": "number",
                "default": "5000", "bounds": [0, 10000], "list": false, "ranges": false, "perCharacter": true, "memory": false, "help": "Récoltes"}}"#,
        );
        assert!(changed(&store, "my-harvest", "events", goal).is_ok());
        let made = &variables()[3];
        assert_eq!(
            (made.default.clone(), made.bounds, made.per_character),
            (serde_json::json!(5000), Some([0, 10000]), true)
        );
        let wrong = parsed(
            r#"{"edit": "variable", "variable": {"id": "objectif", "name": "Objectif", "valueType": "colour",
                "default": null, "bounds": null, "list": false, "ranges": false, "perCharacter": false, "memory": false, "help": ""}}"#,
        );
        assert_eq!(
            changed(&store, "my-harvest", "events", wrong),
            Err(EditError::NotItsType)
        );
        let blank = parsed(r#"{"edit": "newVariable", "name": " "}"#);
        assert_eq!(changed(&store, "my-harvest", "events", blank), Err(EditError::Blank));
        let gone = parsed(r#"{"edit": "deleteVariable", "variable": "objectif"}"#);
        assert!(changed(&store, "my-harvest", "events", gone.clone()).is_ok());
        assert_eq!(
            changed(&store, "my-harvest", "events", gone),
            Err(EditError::NoSuchVariable)
        );
        assert_eq!(variables().len(), 3);
    }

    #[test]
    fn a_character_running_it_follows_its_variables_an_undo_bringing_back_the_variable_not_its_value() {
        let (_folder, store) = mine("ipc-edit-follow");
        let character = crate::storage::Played {
            schema: crate::storage::CHARACTER_SCHEMA,
            id: 1,
            name: "Kalyx".into(),
            level: 54,
            breed: 8,
            sex: None,
            last_seen: 1,
            map_id: None,
            blueprint: Some("my-harvest".into()),
            settings: std::collections::BTreeMap::from([("pods".to_owned(), serde_json::json!(80))]),
            blueprint_version: None,
            dropped: Vec::new(),
        };
        store.remember(&character).unwrap_or_else(|error| panic!("{error}"));
        let own = || store.played().read[0].settings.clone();
        let text = parsed(
            r#"{"edit": "variable", "variable": {"id": "pods", "name": "Empty pods at", "valueType": "text",
                "default": null, "bounds": null, "list": false, "ranges": false, "perCharacter": true, "memory": false, "help": ""}}"#,
        );
        let followed = change(&store, "my-harvest", "events", text).map(|(_, carried)| carried);
        assert_eq!(followed, Ok(true));
        assert!(own().is_empty(), "80 is no text");
        assert_eq!(step(&store, "my-harvest", true).map(|(_, carried)| carried), Ok(false));
        assert_eq!(
            store.blueprints().read[0].variables[1].bounds,
            Some([10, 100]),
            "the variable back"
        );
        assert!(own().is_empty(), "the value stays lost");
        let moved = parsed(r#"{"edit": "move", "nodes": [{"node": "n4", "position": [0, 0]}]}"#);
        assert_eq!(
            change(&store, "my-harvest", "events", moved).map(|(_, carried)| carried),
            Ok(false)
        );
    }

    #[test]
    fn a_blueprint_of_mine_is_renamed_under_its_id_and_one_of_d3bs_is_not() {
        let (folder, store) = mine("ipc-edit-about");
        let about = || parsed(r#"{"edit": "about", "name": "Mes blés", "description": "Le blé d'Astrub."}"#);
        assert!(changed(&store, "my-harvest", "", about()).is_ok());
        let read = &store.blueprints().read[0];
        assert_eq!((read.id.0.as_str(), read.name.as_str()), ("my-harvest", "Mes blés"));
        assert!(
            folder.path().join("blueprints/my-harvest.json").exists(),
            "the same file"
        );
        assert_eq!(
            changed(&store, "automatic-harvest", "", about()),
            Err(EditError::Locked)
        );
        let blank = parsed(r#"{"edit": "about", "name": " ", "description": ""}"#);
        assert_eq!(changed(&store, "my-harvest", "", blank), Err(EditError::Blank));
    }

    #[test]
    fn a_value_that_is_not_json_is_refused_as_not_of_its_type() {
        let (_folder, store) = mine("ipc-edit-value");
        let typed = parsed(r#"{"edit": "value", "node": "n3", "pin": "pods", "value": "eighty"}"#);
        assert_eq!(
            changed(&store, "my-harvest", "events", typed),
            Err(EditError::NotItsType)
        );
        let reset = parsed(r#"{"edit": "value", "node": "n3", "pin": "what", "value": null}"#);
        let drawn = changed(&store, "my-harvest", "events", reset).unwrap_or_else(|error| panic!("{error:?}"));
        let n3 = drawn.graphs[0].nodes.iter().find(|node| node.id == "n3");
        assert!(
            n3.is_some_and(|node| !node.inputs.contains_key("what")),
            "its value dropped"
        );
    }

    #[test]
    fn d3bs_a_missing_one_and_a_refused_edit_change_nothing() {
        let (folder, store) = mine("ipc-edit-refused");
        let path = folder.path().join("blueprints/my-harvest.json");
        let before = std::fs::read_to_string(&path).ok();
        let wrong = || parsed(r#"{"edit": "wire", "from": ["n2", "value"], "to": ["n3", "pods"]}"#);
        for (id, error) in [
            ("automatic-harvest", EditError::Locked),
            ("gone", EditError::NoSuchBlueprint),
            ("my-harvest", EditError::OtherType),
        ] {
            assert_eq!(changed(&store, id, "events", wrong()), Err(error), "{id}");
        }
        assert_eq!(std::fs::read_to_string(&path).ok(), before);
    }

    #[test]
    fn undo_and_redo_give_the_graphs_back_as_they_were_and_refuse_d3bs() {
        let (_folder, store) = mine("ipc-edit-undo");
        let delete = parsed(r#"{"edit": "delete", "nodes": ["n3"], "wires": []}"#);
        assert!(changed(&store, "my-harvest", "events", delete).is_ok());
        let counted = |drawn: Result<BlueprintGraphs, EditError>| {
            let drawn = drawn.unwrap_or_else(|error| panic!("{error:?}"));
            (drawn.graphs[0].nodes.len(), drawn.graphs[0].wires.len())
        };
        assert_eq!(
            counted(stepped(&store, "my-harvest", true)),
            (5, 2),
            "the node and its wires back"
        );
        assert_eq!(counted(stepped(&store, "my-harvest", false)), (4, 0));
        assert_eq!(
            stepped(&store, "automatic-harvest", true).err(),
            Some(EditError::Locked)
        );
    }
}
