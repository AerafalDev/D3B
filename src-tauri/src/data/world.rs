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

    /// The map a click at `x`, `y` on world `world` takes (the owner, 2026-11-17): the one the client draws there, with
    /// the priority on the world map, else the first by id; a house's inside shares its street's coordinates.
    pub fn map_at(&self, world: i32, x: i16, y: i16) -> Option<&MapPosition> {
        let here = self
            .positions
            .iter()
            .filter(|position| position.world_map == world && (position.x, position.y) == (x, y));
        let mut first = None;
        for position in here {
            if position.flags.contains(&MapFlag::HasPriorityOnWorldmap) {
                return Some(position);
            }
            first.get_or_insert(position);
        }
        first
    }

    /// The map of `id`, if the data holds it.
    pub fn position(&self, id: u32) -> Option<&MapPosition> {
        // The positions are in order of map id (the extractor writes them so).
        let at = self.positions.binary_search_by_key(&id, |position| position.id).ok()?;
        self.positions.get(at)
    }

    /// The sub-area of `id`, if the data holds it.
    pub fn sub_area(&self, id: i32) -> Option<&SubArea> {
        let at = self.sub_areas.binary_search_by_key(&id, |sub_area| sub_area.id).ok()?;
        self.sub_areas.get(at)
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

    fn position(id: u32, x: i16, y: i16, priority: bool) -> MapPosition {
        let flags = if priority {
            vec![MapFlag::HasPriorityOnWorldmap]
        } else {
            Vec::new()
        };
        MapPosition {
            id,
            x,
            y,
            sub_area_id: 1,
            world_map: 1,
            name: None,
            flags,
        }
    }

    fn world(positions: Vec<MapPosition>) -> World {
        World {
            positions,
            sub_areas: Vec::new(),
            areas: Vec::new(),
            zaaps: Vec::new(),
        }
    }

    #[test]
    fn a_click_takes_the_map_with_the_priority_there_else_the_first_by_id_on_that_world() {
        let shared = world(vec![
            position(10, 5, -18, false),
            position(11, 5, -18, true),
            position(12, 6, -18, false),
        ]);
        assert_eq!(shared.map_at(1, 5, -18).map(|it| it.id), Some(11));
        assert_eq!(shared.map_at(1, 6, -18).map(|it| it.id), Some(12));
        assert_eq!(shared.map_at(2, 5, -18).map(|it| it.id), None, "another world");
        assert_eq!(shared.map_at(1, 0, 0).map(|it| it.id), None);
        let inside = world(vec![position(20, 5, -18, false), position(21, 5, -18, false)]);
        assert_eq!(inside.map_at(1, 5, -18).map(|it| it.id), Some(20));
    }

    #[test]
    fn a_map_is_found_by_its_id() {
        let maps = world(vec![position(10, 5, -18, false), position(12, 6, -18, false)]);
        assert_eq!(maps.position(12).map(|it| (it.x, it.y)), Some((6, -18)));
        assert!(maps.position(11).is_none());
    }

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
