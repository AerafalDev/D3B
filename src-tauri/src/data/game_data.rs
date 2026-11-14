use std::path::Path;

use super::breeds::{self, Breed};
use super::graph::WorldGraph;
use super::items::ItemTables;
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
    /// The ways between maps.
    pub graph: WorldGraph,
    /// The breeds, in order of id, whose heads the webview loads from the folder.
    pub breeds: Vec<Breed>,
    /// The items, their types, the jobs and their skills.
    pub items: ItemTables,
}

impl GameData {
    /// Reads the data folder `folder`, refusing it whole when any of its tables cannot be read.
    pub fn load(folder: &Path) -> Result<Self, DataError> {
        let manifest = Manifest::load(folder)?;
        let world = World::load(folder, &manifest)?;
        let worlds = manifest.read(folder, "worlds")?;
        let graph = WorldGraph::load(folder, &manifest)?;
        let breeds = breeds::load(folder, &manifest)?;
        let items = ItemTables::load(folder, &manifest)?;
        Ok(Self {
            manifest,
            world,
            worlds,
            graph,
            breeds,
            items,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_a_folder_of_the_tables_the_extractor_writes() {
        // The C# tests write these fixtures byte for byte: this checks the names the app and the extractor agree on.
        let fixtures = Path::new(env!("CARGO_MANIFEST_DIR")).join("../tools/extractor/tests/fixtures/world");
        let folder = std::env::temp_dir().join(format!("d3b-data-test-folder-{}", std::process::id()));
        std::fs::create_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        let names = [
            "map-positions",
            "sub-areas",
            "areas",
            "zaaps",
            "worlds",
            "world-graph-vertices",
            "world-graph-edges",
        ];
        let mut tables = Vec::new();
        for name in names {
            std::fs::copy(
                fixtures.join(format!("{name}.json")),
                folder.join(format!("{name}.json")),
            )
            .unwrap_or_else(|error| panic!("{error}"));
            tables.push(format!(r#"{{"name":"{name}","path":"{name}.json","sha256":"00"}}"#));
        }
        std::fs::copy(fixtures.join("../breeds/breeds.json"), folder.join("breeds.json"))
            .unwrap_or_else(|error| panic!("{error}"));
        tables.push(r#"{"name":"breeds","path":"breeds.json","sha256":"00"}"#.to_owned());
        for name in ["items", "item-types", "jobs", "skills"] {
            std::fs::copy(
                fixtures.join(format!("../items/{name}.json")),
                folder.join(format!("{name}.json")),
            )
            .unwrap_or_else(|error| panic!("{error}"));
            tables.push(format!(r#"{{"name":"{name}","path":"{name}.json","sha256":"00"}}"#));
        }
        for picture in ["heads/8-0", "heads/8-1", "heads/9-0", "heads/9-1", "icons/jobs/1"] {
            tables.push(format!(
                r#"{{"name":"{picture}","path":"{picture}.png","sha256":"00"}}"#
            ));
        }
        let manifest = format!(
            r#"{{"format":{},"build":"3.7.13.17","extractor":"0","tables":[{}]}}"#,
            super::super::FORMAT,
            tables.join(",")
        );
        std::fs::write(folder.join(Manifest::FILE), manifest).unwrap_or_else(|error| panic!("{error}"));
        let data = GameData::load(&folder);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        let data = data.unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(data.manifest.build, "3.7.13.17");
        assert_eq!(data.world.zaaps.len(), 1);
        assert_eq!(data.graph.edges.len(), 1);
        assert_eq!(data.breeds.len(), 2);
        assert_eq!(data.items.skills.len(), 2);
    }

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
        assert_ne!(data.graph.edges.len(), 0);
        assert_ne!(data.breeds.len(), 0);
        assert_ne!(data.items.items.len(), 0);
        assert_ne!(data.items.skills.len(), 0);
        assert_ne!(data.items.item_icons.len(), 0);
    }
}
