//! What the last local replay of the owner's captures read, per build (`src-tauri/protocol/replay/<build>.json`):
//! counts by readable name and outcome, never a frame's content. The protocol's replay writes it, the report reads it.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

/// The counts of one build's game connections replayed.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub(crate) struct Replayed {
    pub(crate) build: String,
    pub(crate) connections: u64,
    pub(crate) frames: u64,
    /// By readable name, for the pinned messages only: an unpinned message has no name of ours.
    pub(crate) messages: BTreeMap<String, Tally>,
}

/// How a pinned message's frames read.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub(crate) struct Tally {
    pub(crate) understood: u64,
    pub(crate) read: u64,
    pub(crate) not_trusted: u64,
}

/// A replay's file that cannot be read.
#[derive(Debug, thiserror::Error)]
pub(crate) enum ReplayedError {
    #[error("{}: {reason}", path.display())]
    Shape { path: PathBuf, reason: String },
    #[error("{} names build {named}", path.display())]
    Build { path: PathBuf, named: String },
    #[error("{} cannot be read: {reason}", path.display())]
    Unreadable { path: PathBuf, reason: String },
}

/// The folder of the replays' files.
pub(crate) fn folder() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("protocol").join("replay")
}

/// The file of `build` in `folder`.
fn path(folder: &Path, build: &str) -> PathBuf {
    folder.join(format!("{build}.json"))
}

/// The last replay of `build` in `folder`; `None` when it was never replayed.
pub(crate) fn read(folder: &Path, build: &str) -> Result<Option<Replayed>, ReplayedError> {
    let path = path(folder, build);
    let json = match std::fs::read_to_string(&path) {
        Ok(json) => json,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(ReplayedError::Unreadable {
                path,
                reason: error.to_string(),
            });
        }
    };
    let replayed: Replayed = serde_json::from_str(&json).map_err(|error| ReplayedError::Shape {
        path: path.clone(),
        reason: error.to_string(),
    })?;
    if replayed.build != build {
        return Err(ReplayedError::Build {
            path,
            named: replayed.build,
        });
    }
    Ok(Some(replayed))
}

/// Writes `replayed` as its build's file in `folder`, in the form Biome keeps.
pub(crate) fn write(folder: &Path, replayed: &Replayed) -> std::io::Result<()> {
    let json = serde_json::to_string_pretty(replayed).map_err(std::io::Error::other)?;
    std::fs::create_dir_all(folder)?;
    std::fs::write(path(folder, &replayed.build), json + "\n")
}
