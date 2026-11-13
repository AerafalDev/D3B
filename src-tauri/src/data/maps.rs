use std::collections::BTreeMap;
use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;

/// The number of cells of every map: 14 columns of 40 staggered rows.
pub const CELL_COUNT: usize = 560;

/// How many files the maps are split in, by the last two digits of their id (ADR 0008).
pub const SHARDS: u32 = 100;

/// The bits of a cell's `flags`, in the order the extractor packs the cell's booleans (docs/data.md).
pub mod cell {
    /// `mov`: a character may walk on the cell.
    pub const WALKABLE: u16 = 1 << 0;
    /// `los`: the cell lets lines of sight through.
    pub const LINE_OF_SIGHT: u16 = 1 << 1;
    pub const NON_WALKABLE_DURING_FIGHT: u16 = 1 << 2;
    pub const NON_WALKABLE_DURING_ROLEPLAY: u16 = 1 << 3;
    pub const FARM: u16 = 1 << 4;
    pub const VISIBLE: u16 = 1 << 5;
    pub const HAVENBAG: u16 = 1 << 6;
    pub const MONSTERS_BLOCKED: u16 = 1 << 7;
    pub const RED: u16 = 1 << 8;
    pub const BLUE: u16 = 1 << 9;
}

/// One map of the client: its neighbours, its cells as columns, its interactive elements (3.7.13.17,
/// `ClientMapData` and `MapMetadata`).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MapData {
    pub id: u32,
    pub top: i32,
    pub bottom: i32,
    pub left: i32,
    pub right: i32,
    /// Each cell's booleans, the bits of [`cell`].
    pub flags: Vec<u16>,
    /// Each cell's `linkedZone`; its high four bits are the cell's roleplay zone, the world graph's.
    pub linked_zone: Vec<u8>,
    /// `mapChangeData` where it is set: bit `d` where the cell leaves the map in direction `d`, clockwise from east.
    pub map_change: BTreeMap<u16, i32>,
    /// The cell's altitude where it is not zero.
    pub floor: BTreeMap<u16, i32>,
    pub move_zone: BTreeMap<u16, i32>,
    pub arrow: BTreeMap<u16, i32>,
    pub speed: BTreeMap<u16, i32>,
    /// The interactive elements, in order of id.
    pub elements: Vec<MapElement>,
}

/// An interactive element of a map: where it stands and the graphic it is drawn with.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MapElement {
    pub id: i32,
    /// Its cell, or -1 for an element drawn by a background group, which has none.
    pub cell_id: i32,
    pub gfx_id: i32,
}

/// The name, in the manifest, of the file that holds the map `id`.
#[must_use]
pub fn shard_of(id: u32) -> String {
    format!("maps/{}", id % SHARDS)
}

/// Reads the map `id` from its file of the data folder `folder`, or `None` when the client holds no such map.
pub fn read_map(folder: &Path, manifest: &Manifest, id: u32) -> Result<Option<MapData>, DataError> {
    Ok(read_shard(folder, manifest, id % SHARDS)?
        .into_iter()
        .find(|map| map.id == id))
}

/// Reads the maps of the file `shard` (0 to 99) of the data folder `folder`.
pub fn read_shard(folder: &Path, manifest: &Manifest, shard: u32) -> Result<Vec<MapData>, DataError> {
    let maps: Vec<MapData> = manifest.read(folder, &format!("maps/{shard}"))?;
    for map in &maps {
        if map.flags.len() != CELL_COUNT || map.linked_zone.len() != CELL_COUNT {
            return Err(DataError::Cells {
                map: map.id,
                found: map.flags.len().min(map.linked_zone.len()),
                expected: CELL_COUNT,
            });
        }
    }
    Ok(maps)
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write this file byte for byte (tools/extractor/tests/Maps/MapTablesTests.cs).
    const MAPS: &str = include_str!("../../../tools/extractor/tests/fixtures/maps/7.json");

    #[test]
    fn reads_the_maps_the_extractor_writes() {
        let maps: Vec<MapData> = serde_json::from_str(MAPS).unwrap_or_else(|error| panic!("{error}"));
        let map = &maps[0];
        assert_eq!(map.id, 191_106_007);
        assert_eq!(map.flags.len(), CELL_COUNT);
        assert_eq!(map.flags[0], cell::WALKABLE | cell::LINE_OF_SIGHT | cell::VISIBLE);
        assert_eq!(map.linked_zone[0] >> 4, 1);
        assert_eq!(map.map_change.get(&0), Some(&131));
        assert_eq!(
            map.elements[0],
            MapElement {
                id: 465_453,
                cell_id: 300,
                gfx_id: 7_506
            }
        );
    }

    #[test]
    fn packs_each_boolean_of_a_cell_in_a_bit_of_its_own() {
        let bits = [
            cell::WALKABLE,
            cell::LINE_OF_SIGHT,
            cell::NON_WALKABLE_DURING_FIGHT,
            cell::NON_WALKABLE_DURING_ROLEPLAY,
            cell::FARM,
            cell::VISIBLE,
            cell::HAVENBAG,
            cell::MONSTERS_BLOCKED,
            cell::RED,
            cell::BLUE,
        ];
        assert_eq!(bits.iter().fold(0, |all, bit| all | bit), 0b11_1111_1111);
    }

    #[test]
    fn refuses_a_map_without_its_560_cells() {
        let folder = std::env::temp_dir().join(format!("d3b-data-test-cells-{}", std::process::id()));
        std::fs::create_dir_all(folder.join("maps")).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(
            folder.join("maps/7.json"),
            MAPS.replacen("\"flags\":[35,", "\"flags\":[", 1),
        )
        .unwrap_or_else(|error| panic!("{error}"));
        let manifest = Manifest::parse(
            r#"{"format":2,"build":"0","extractor":"0","tables":[{"name":"maps/7","path":"maps/7.json","sha256":"00"}]}"#,
        )
        .unwrap_or_else(|error| panic!("{error}"));
        let read = read_shard(&folder, &manifest, 7);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(
            read,
            Err(DataError::Cells {
                map: 191_106_007,
                found: 559,
                ..
            })
        ));
    }

    #[test]
    fn reads_a_map_from_its_file_or_says_it_has_none() {
        let folder = std::env::temp_dir().join(format!("d3b-data-test-map-{}", std::process::id()));
        std::fs::create_dir_all(folder.join("maps")).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(folder.join("maps/7.json"), MAPS).unwrap_or_else(|error| panic!("{error}"));
        let manifest = Manifest::parse(
            r#"{"format":2,"build":"0","extractor":"0","tables":[{"name":"maps/7","path":"maps/7.json","sha256":"00"}]}"#,
        )
        .unwrap_or_else(|error| panic!("{error}"));

        let found = read_map(&folder, &manifest, 191_106_007);
        let absent = read_map(&folder, &manifest, 107);
        // Its file is not in the manifest.
        let no_file = read_map(&folder, &manifest, 191_106_008);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(found.ok().flatten().map(|map| map.id), Some(191_106_007));
        assert!(matches!(absent, Ok(None)));
        assert!(no_file.is_err());
    }

    #[test]
    fn names_the_file_of_a_map_by_the_last_two_digits_of_its_id() {
        assert_eq!(shard_of(191_106_048), "maps/48");
        assert_eq!(shard_of(7), "maps/7");
    }

    #[test]
    fn reads_every_map_the_app_ships_when_there_is_one() {
        let folder = Path::new(env!("CARGO_MANIFEST_DIR")).join("../data");
        let Ok(manifest) = Manifest::load(&folder) else {
            return;
        };
        let count: usize = (0..SHARDS)
            .map(|shard| read_shard(&folder, &manifest, shard).map(|maps| maps.len()))
            .sum::<Result<usize, _>>()
            .unwrap_or_else(|error| panic!("{error}"));
        assert_ne!(count, 0);
    }
}
