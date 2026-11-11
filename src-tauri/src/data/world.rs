use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;
use super::map_flag::MapFlag;
use super::text::Text;

/// Where a map stands in the world (`MapInformationData`, 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MapPosition {
    pub id: u32,
    pub x: i16,
    pub y: i16,
    pub sub_area_id: i32,
    /// The world whose image the map is drawn on, -1 for none.
    pub world_map: i32,
    /// The map's own name, which most maps lack.
    pub name: Option<Text>,
    pub flags: Vec<MapFlag>,
}

/// A sub-area of the world (`SubAreaData`, 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SubArea {
    pub id: i32,
    pub name: Option<Text>,
    pub area_id: i32,
    pub level: u32,
    /// The map of its zaap, if it has one.
    pub zaap_map_id: Option<i32>,
    pub basic_account_allowed: bool,
    /// The world it is drawn on, -1 when it follows its area's.
    pub custom_world_map_id: i32,
    /// The items its `harvestables` lists, loot boxes among resources, as the client lists them.
    pub harvestables: Vec<i32>,
    /// The monsters that live there, as the client lists them.
    pub monsters: Vec<i32>,
}

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
    /// Every map position, in order of map id.
    pub positions: Vec<MapPosition>,
    /// Every sub-area, in order of id.
    pub sub_areas: Vec<SubArea>,
    /// Every area, in order of id.
    pub areas: Vec<Area>,
    /// Every zaap, in order of id.
    pub zaaps: Vec<Zaap>,
}

impl World {
    /// Reads the world's tables that `manifest` lists in `folder`.
    pub fn load(folder: &Path, manifest: &Manifest) -> Result<Self, DataError> {
        Ok(Self {
            positions: manifest.read(folder, "map-positions")?,
            sub_areas: manifest.read(folder, "sub-areas")?,
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
    const POSITIONS: &str = include_str!("../../../tools/extractor/tests/fixtures/world/map-positions.json");
    const SUB_AREAS: &str = include_str!("../../../tools/extractor/tests/fixtures/world/sub-areas.json");

    #[test]
    fn reads_the_positions_the_extractor_writes() {
        let positions: Vec<MapPosition> = serde_json::from_str(POSITIONS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!((positions[0].x, positions[0].y), (-1, -27));
        assert_eq!(
            positions[0].flags,
            [MapFlag::CapabilityAllowTeleportTo, MapFlag::CapabilityAllowTeleportFrom]
        );
    }

    #[test]
    fn refuses_a_flag_it_does_not_know() {
        let text = POSITIONS.replace("capabilityAllowTeleportTo", "capabilityAllowFlying");
        assert!(serde_json::from_str::<Vec<MapPosition>>(&text).is_err());
    }

    #[test]
    fn reads_the_sub_areas_the_extractor_writes() {
        let sub_areas: Vec<SubArea> = serde_json::from_str(SUB_AREAS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(sub_areas[0].zaap_map_id, Some(191_106_048));
        assert_eq!(sub_areas[1].zaap_map_id, None);
        assert_eq!(sub_areas[0].monsters, [31, 47]);
    }

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
