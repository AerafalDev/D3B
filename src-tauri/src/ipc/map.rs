//! A map's cells, as the live view draws them (`docs/game.md`, "In the app"): read from the game data's maps on demand,
//! one file at a time, never at start (`docs/data.md`).

use serde::{Deserialize, Serialize};
use specta::Type;

use super::LoadedData;
use crate::data::MapData;

/// What the live view draws of a map: its 560 cells and its interactive elements.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct MapCells {
    /// Each cell's flags, as `docs/data.md` packs them: bit 0 walkable, bit 1 line of sight.
    pub flags: Vec<u16>,
    /// The cells that lead to another map.
    pub exits: Vec<u16>,
    /// The interactive elements, each with its cell, or −1 for one drawn without a cell.
    pub elements: Vec<ElementAt>,
}

/// An interactive element of the map and its cell.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
pub struct ElementAt {
    pub id: i32,
    pub cell: i32,
}

impl MapCells {
    fn new(map: MapData) -> Self {
        Self {
            flags: map.flags,
            exits: map
                .map_change
                .into_iter()
                .filter(|&(_, directions)| directions != 0)
                .map(|(cell, _)| cell)
                .collect(),
            elements: map
                .elements
                .into_iter()
                .map(|element| ElementAt {
                    id: element.id,
                    cell: element.cell_id,
                })
                .collect(),
        }
    }
}

/// The cells of the map `map_id` from the packed game data, read off the main thread; why not, when there is no data
/// or no such map.
#[tauri::command]
#[specta::specta]
pub async fn map_cells(map_id: u32, data: tauri::State<'_, LoadedData>) -> Result<MapCells, String> {
    let (Ok(game), Some(folder)) = (&data.0, &data.1) else {
        return Err("no game data".into());
    };
    let (manifest, folder) = (game.manifest.clone(), folder.clone());
    let read = tauri::async_runtime::spawn_blocking(move || crate::data::read_map(&folder, &manifest, map_id));
    match read.await.map_err(|error| error.to_string())? {
        Ok(Some(map)) => Ok(MapCells::new(map)),
        Ok(None) => Err(format!("no map {map_id} in the game data")),
        Err(error) => Err(error.to_string()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write this file byte for byte (tools/extractor/tests/Maps/MapTablesTests.cs).
    const MAPS: &str = include_str!("../../../tools/extractor/tests/fixtures/maps/7.json");

    #[test]
    fn draws_a_maps_cells_its_exits_and_its_elements() {
        let maps: Vec<MapData> = serde_json::from_str(MAPS).unwrap_or_else(|error| panic!("{error}"));
        let map = maps.into_iter().next().unwrap_or_else(|| panic!("no map"));
        let changes = map.map_change.len();

        let cells = MapCells::new(map);

        assert_eq!(cells.flags.len(), 560);
        assert!(cells.exits.contains(&0));
        assert!(cells.exits.len() <= changes);
        assert_eq!(cells.elements[0], ElementAt { id: 465_453, cell: 300 });
    }
}
