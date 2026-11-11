use std::path::Path;

use super::manifest::{DataError, Manifest};
use super::world::World;
use super::world_maps::WorldMap;

/// Everything a data folder holds, read and checked once when the app starts.
#[derive(Debug, Clone, PartialEq)]
pub struct GameData {
    /// What the folder holds, and the client build it comes from.
    pub manifest: Manifest,
    /// The world's tables.
    pub world: World,
    /// The worlds of the map, whose tiles the webview loads from the folder.
    pub worlds: Vec<WorldMap>,
}

impl GameData {
    /// Reads the data folder `folder`, refusing it whole when any of its tables cannot be read.
    pub fn load(folder: &Path) -> Result<Self, DataError> {
        let manifest = Manifest::load(folder)?;
        let world = World::load(folder, &manifest)?;
        let worlds = manifest.read(folder, "worlds")?;
        Ok(Self {
            manifest,
            world,
            worlds,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_the_folder_the_app_ships_when_there_is_one() {
        // `data/` holds a real extraction only on a machine that fetched or extracted one: elsewhere there is nothing to read.
        let folder = Path::new(env!("CARGO_MANIFEST_DIR")).join("../data");
        if !folder.join(Manifest::FILE).is_file() {
            return;
        }
        let data = GameData::load(&folder).unwrap_or_else(|error| panic!("{error}"));
        assert_ne!(data.world.areas.len(), 0);
        assert_ne!(data.world.zaaps.len(), 0);
        assert_ne!(data.world.positions.len(), 0);
        assert_ne!(data.world.sub_areas.len(), 0);
        assert_ne!(data.worlds.len(), 0);
    }
}
