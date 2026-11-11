use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;
use super::text::Text;

/// An area of the world, such as Astrub (`AreaData`, 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Area {
    pub id: i32,
    /// Its name, or `None` for the areas the client names text 0.
    pub name: Option<Text>,
    pub super_area_id: i32,
    /// The world the area is drawn on when `has_world_map`.
    pub worldmap_id: i32,
    pub has_world_map: bool,
}

/// A zaap: the map it stands on (`WaypointData`, 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Zaap {
    pub id: i32,
    pub map_id: i32,
    pub sub_area_id: i32,
    pub activated: bool,
}

/// The world's tables of a data folder.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct World {
    /// Every area, in order of id.
    pub areas: Vec<Area>,
    /// Every zaap, in order of id.
    pub zaaps: Vec<Zaap>,
}

impl World {
    /// Reads the world's tables that `manifest` lists in `folder`.
    pub fn load(folder: &Path, manifest: &Manifest) -> Result<Self, DataError> {
        Ok(Self {
            areas: manifest.read(folder, "areas")?,
            zaaps: manifest.read(folder, "zaaps")?,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/World/WorldTablesTests.cs).
    const AREAS: &str = include_str!("../../../tools/extractor/tests/fixtures/world/areas.json");
    const ZAAPS: &str = include_str!("../../../tools/extractor/tests/fixtures/world/zaaps.json");

    #[test]
    fn reads_the_areas_the_extractor_writes() {
        let areas: Vec<Area> = serde_json::from_str(AREAS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(areas.len(), 2);
        assert_eq!(areas[0].name.as_ref().map(|name| name.pt.as_str()), Some("Astrub"));
        assert_eq!(areas[1].name, None);
        assert!(areas[1].has_world_map);
    }

    #[test]
    fn reads_the_zaaps_the_extractor_writes() {
        let zaaps: Vec<Zaap> = serde_json::from_str(ZAAPS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(zaaps[0].map_id, 191_106_048);
    }

    #[test]
    fn refuses_a_field_it_does_not_know() {
        let text = ZAAPS.replace("\"activated\"", "\"unknown\":0,\"activated\"");
        assert!(serde_json::from_str::<Vec<Zaap>>(&text).is_err());
    }
}
