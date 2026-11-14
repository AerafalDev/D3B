//! The game data packed in the app.
//!
//! The .NET extractor (`tools/extractor`) writes one folder per client build: a `manifest.json` that lists every file
//! with its SHA-256, the JSON tables (the large ones in shards), the world map's tiles and the breeds' heads. A `client-<build>` release
//! asset carries that folder; the release workflow puts it in `data/`, which Tauri bundles as a resource. Nothing
//! extracted from the client is ever committed (`docs/data.md`).

mod breeds;
mod game_data;
mod graph;
#[cfg_attr(
    not(test),
    expect(
        dead_code,
        reason = "D3B's blueprints per harvesting profession and the items' picker read them from step 3's PR 4"
    )
)]
mod items;
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

pub use breeds::{Breed, head_file};
pub use game_data::GameData;
pub use manifest::{DataError, Manifest};
pub use maps::{MapData, read_map};
pub use world::{MapPosition, World};

/// The format of the folder this version of the code reads. The extractor writes the same number in the manifest
/// (`DataFolder.Format`); a different one is refused rather than half read. Format 2 adds the breeds and their heads,
/// format 3 the items, their types, the jobs and their skills.
pub const FORMAT: u32 = 3;

#[cfg(test)]
mod tests {
    #[test]
    fn the_release_fetches_the_data_of_this_format() {
        // The release workflow and `data:fetch` download the asset of the format `.github/game-data.json` names.
        let text = include_str!("../../../.github/game-data.json");
        let config: serde_json::Value = serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(config["format"].as_u64(), Some(u64::from(super::FORMAT)));
    }
}
