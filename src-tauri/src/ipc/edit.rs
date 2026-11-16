//! The editor's changes to a blueprint of mine (`docs/bot.md`, "In the app"; `bot::edit`), each saved as it is made
//! (the owner, 2026-11-16): the blueprint's graphs given back, with their problems, as `blueprint_graphs` gives them.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;

use super::blueprints::PlayersFiles;
use super::graph::{BlueprintGraphs, NodeDoes, PinSide, graphs};
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
    /// A node put at `position`.
    Move { node: String, position: [i32; 2] },
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
    NoSuchPin,
    WrongWay,
    OtherType,
    SameNode,
    NotAChoice,
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
            Refusal::NoSuchPin => Self::NoSuchPin,
            Refusal::WrongWay => Self::WrongWay,
            Refusal::OtherType => Self::OtherType,
            Refusal::SameNode => Self::SameNode,
            Refusal::NotAChoice => Self::NotAChoice,
        }
    }
}

fn end([node, pin]: [String; 2]) -> End {
    End(Id(node), Id(pin))
}

impl From<GraphEdit> for bot::Edit {
    fn from(edit: GraphEdit) -> Self {
        match edit {
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
            GraphEdit::Move { node, position } => Self::Move {
                node: Id(node),
                position,
            },
        }
    }
}

/// Makes `edit` in the graph `graph` of my blueprint `id` and saves it; its graphs then.
fn change(store: &Store, id: &str, graph: &str, edit: GraphEdit) -> Result<BlueprintGraphs, EditError> {
    let graph = Id(graph.to_owned());
    let blueprint = store.change_blueprint(id, |blueprint| {
        bot::edit(blueprint, &graph, edit.into()).map_err(EditError::from)
    })?;
    Ok(graphs(&blueprint))
}

/// Makes `edit` in the graph `graph` of my blueprint `id`, saved at once; the blueprint's graphs and problems then.
#[tauri::command]
#[specta::specta]
pub async fn edit_blueprint(
    app: tauri::AppHandle,
    id: String,
    graph: String,
    edit: GraphEdit,
) -> Result<BlueprintGraphs, EditError> {
    let done = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        change(files.store().ok_or(EditError::NoFolder)?, &id, &graph, edit)
    });
    done.await.unwrap_or_else(|error| {
        Err(EditError::NotSaved {
            reason: error.to_string(),
        })
    })
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
        let drawn = change(&store, "my-harvest", "events", add).unwrap_or_else(|error| panic!("{error:?}"));
        let events = &drawn.graphs[0];
        let notify = events.nodes.iter().find(|node| node.id == "notify");
        assert_eq!(notify.and_then(|node| node.position), Some([400, 120]));
        let wire = ["n3".to_owned(), "failed".to_owned()];
        assert!(events.wires.iter().any(|it| it.from == wire && it.to[0] == "notify"));
        assert_eq!(store.blueprints().read[0].graphs[0].nodes.len(), 6, "written at once");
        let wire = parsed(r#"{"edit": "wire", "from": ["n3", "done"], "to": ["n4", "run"]}"#);
        assert!(change(&store, "my-harvest", "events", wire).is_ok());
        let moved = parsed(r#"{"edit": "move", "node": "n4", "position": [-5, 9]}"#);
        let drawn = change(&store, "my-harvest", "events", moved).unwrap_or_else(|error| panic!("{error:?}"));
        let n4 = drawn.graphs[0].nodes.iter().find(|node| node.id == "n4");
        assert_eq!(
            (n4.and_then(|node| node.position), drawn.graphs[0].wires.len()),
            (Some([-5, 9]), 4)
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
            assert_eq!(change(&store, id, "events", wrong()), Err(error), "{id}");
        }
        assert_eq!(std::fs::read_to_string(&path).ok(), before);
    }
}
