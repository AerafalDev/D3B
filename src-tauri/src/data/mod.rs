//! The game data packed in the app.
//!
//! The .NET extractor (`tools/extractor`) writes one folder per client build: a `manifest.json` that lists every file
//! with its SHA-256, the JSON tables (the large ones in shards) and the world map's tiles. A `client-<build>` release
//! asset carries that folder; the release workflow puts it in `data/`, which Tauri bundles as a resource. Nothing
//! extracted from the client is ever committed (`docs/data.md`).

mod game_data;
mod graph;
mod manifest;
mod map_flag;
#[cfg_attr(
    not(test),
    expect(
        dead_code,
        reason = "the live view reads a map whole; the cells' bits are read by the paths of step 4"
    )
)]
mod maps;
mod text;
mod world;
mod world_maps;

pub use game_data::GameData;
pub use manifest::{DataError, Manifest};
pub use maps::{MapData, read_map};
pub use world::{MapPosition, World};

/// The format of the folder this version of the code reads. The extractor writes the same number in the manifest
/// (`DataFolder.Format`); a different one is refused rather than half read.
pub const FORMAT: u32 = 1;
