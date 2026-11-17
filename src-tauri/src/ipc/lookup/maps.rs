//! Maps found for the editor's map field (the owner, 2026-11-17): the map a click on the World takes, and the maps a
//! value already names, each by its coordinates, world and sub-area.

use serde::Serialize;
use specta::Type;
use tauri::Manager;

use super::super::atlas::AtlasError;
use super::super::character::Names;
use super::super::commands::LoadedData;
use crate::data::{MapPosition, World};

/// A map as the field shows it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct MapView {
    pub id: u32,
    pub at: [i16; 2],
    /// The world it is drawn on, -1 for none.
    pub world: i32,
    pub sub_area: Option<Names>,
}

fn view(world: &World, position: &MapPosition) -> MapView {
    let sub_area = world.sub_area(position.sub_area_id).and_then(|it| it.name.as_ref());
    MapView {
        id: position.id,
        at: [position.x, position.y],
        world: position.world_map,
        sub_area: sub_area.map(|name| Names {
            fr: name.fr.clone(),
            en: name.en.clone(),
        }),
    }
}

/// The world of the data loaded, or why there is none.
fn loaded(data: &LoadedData) -> Result<&World, AtlasError> {
    data.0
        .as_ref()
        .map(|game| &game.world)
        .map_err(|reason| AtlasError::NoData { reason: reason.clone() })
}

/// The map a click at `x`, `y` on `world` takes: the one with the priority there, else the first by id; `None` where
/// no map is.
#[tauri::command]
#[specta::specta]
pub async fn map_at(app: tauri::AppHandle, world: i32, x: i16, y: i16) -> Result<Option<MapView>, AtlasError> {
    let data = app.state::<LoadedData>();
    let world_data = loaded(&data)?;
    Ok(world_data
        .map_at(world, x, y)
        .map(|position| view(world_data, position)))
}

/// The maps of `ids` the data holds, in that order: what a value already chosen names.
#[tauri::command]
#[specta::specta]
pub async fn find_maps(app: tauri::AppHandle, ids: Vec<u32>) -> Result<Vec<MapView>, AtlasError> {
    let data = app.state::<LoadedData>();
    let world = loaded(&data)?;
    Ok(ids
        .iter()
        .filter_map(|id| world.position(*id))
        .map(|position| view(world, position))
        .collect())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn without_game_data_a_map_says_why() {
        let none = LoadedData(Err("no data folder".into()), None);
        assert_eq!(
            loaded(&none).err(),
            Some(AtlasError::NoData {
                reason: "no data folder".into()
            })
        );
    }

    #[test]
    fn a_map_is_shown_by_its_coordinates_world_and_sub_area_named() {
        let read = |text: &str| serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
        let world = World {
            positions: read(include_str!(
                "../../../../tools/extractor/tests/fixtures/world/map-positions.json"
            )),
            sub_areas: Vec::new(),
            areas: Vec::new(),
            zaaps: Vec::new(),
        };
        let first = &world.positions[0];
        let shown = view(&world, first);
        assert_eq!((shown.id, shown.at, shown.sub_area), (first.id, [-1, -27], None));
    }
}
