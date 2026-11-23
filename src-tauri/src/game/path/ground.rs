//! What of a map's cells a walk out of fights depends on: which may be walked on, and their floors and stairs.

use crate::data::{CELL_COUNT, MapData, cell};

use super::grid::{self, DIRECTIONS};

/// The most a step off a stair may change floor (inferred, the C# version measured on 3.7.12.12, `main` D86: every
/// step of 10 walked; steps of 20 to 130 into or off a stair, or between two cells of moveZone 0, refused).
const MOST_FLOOR_CHANGE: i32 = 10;

/// A map's ground, as a walk out of fights meets it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Ground {
    walkable: Vec<bool>,
    floor: Vec<i32>,
    move_zone: Vec<i32>,
    /// Whether the map holds both moveZones: only there a stair's cells, of moveZone 1, exist and floors count.
    stairs: bool,
}

impl Ground {
    /// The ground of `map`. A cell is walkable when the client marks it so and not as closed out of fights
    /// (`nonWalkableDuringRP`, 101 cells of 3.7.12.12, the C# version's `MapCell.IsWalkable`).
    pub(crate) fn of(map: &MapData) -> Self {
        let walkable = (0..CELL_COUNT)
            .map(|index| {
                map.flags.get(index).is_some_and(|flags| {
                    flags & (cell::WALKABLE | cell::NON_WALKABLE_DURING_ROLEPLAY) == cell::WALKABLE
                })
            })
            .collect();
        let sparse = |values: &std::collections::BTreeMap<u16, i32>| -> Vec<i32> {
            (0..CELL_COUNT)
                .map(|index| {
                    u16::try_from(index)
                        .ok()
                        .and_then(|index| values.get(&index).copied())
                        .unwrap_or(0)
                })
                .collect()
        };
        let move_zone = sparse(&map.move_zone);
        // Client 3.7.12.12 holds moveZones 0 and 1 only: a map of one of them has no stair, and its floors never count.
        let stairs = move_zone.iter().any(|zone| *zone != move_zone[0]);
        Self {
            walkable,
            floor: sparse(&map.floor),
            move_zone,
            stairs,
        }
    }

    /// Whether a character may stand on `cell` out of fights.
    pub(crate) fn walkable(&self, cell: u16) -> bool {
        self.walkable.get(usize::from(cell)).copied().unwrap_or(false)
    }

    /// The cell a step from `from` in `direction` reaches, when a character may take it.
    ///
    /// The step needs a walkable cell; a step across (east, south, west, north) passes between two cells, one of which
    /// at least is walkable (inferred, `main` D68: the client's paths never passed between two that are not). On a map
    /// with stairs, a step along a stair may change floor as much as it does, any other at most 10 (D86).
    pub(crate) fn step(&self, from: u16, direction: u8) -> Option<u16> {
        let next = grid::neighbour(from, direction)?;
        if !self.walkable(next) || !self.crosses(from, direction) || !self.climbs(from, next) {
            return None;
        }
        Some(next)
    }

    fn crosses(&self, from: u16, direction: u8) -> bool {
        if !grid::is_across(direction) {
            return true;
        }
        let side = |turn: u8| grid::neighbour(from, (direction + turn) % DIRECTIONS).is_some_and(|cell| self.walkable(cell));
        side(DIRECTIONS - 1) || side(1)
    }

    fn climbs(&self, from: u16, to: u16) -> bool {
        if !self.stairs {
            return true;
        }
        let (from, to) = (usize::from(from), usize::from(to));
        (self.move_zone[from] == self.move_zone[to] && self.move_zone[from] != 0)
            || (self.floor[from] - self.floor[to]).abs() <= MOST_FLOOR_CHANGE
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use std::collections::BTreeMap;

    use super::*;

    /// A map of `walkable` cells only, which tests then shape.
    pub(crate) fn open_map() -> MapData {
        MapData {
            id: 1,
            top: 0,
            bottom: 0,
            left: 0,
            right: 0,
            flags: vec![cell::WALKABLE; CELL_COUNT],
            linked_zone: vec![1 << 4; CELL_COUNT],
            map_change: BTreeMap::new(),
            floor: BTreeMap::new(),
            move_zone: BTreeMap::new(),
            arrow: BTreeMap::new(),
            speed: BTreeMap::new(),
            elements: Vec::new(),
        }
    }

    #[test]
    fn walks_on_the_cells_the_client_marks_walkable_out_of_fights() {
        let mut map = open_map();
        map.flags[30] = 0;
        map.flags[31] = cell::WALKABLE | cell::NON_WALKABLE_DURING_ROLEPLAY;
        map.flags[32] = cell::WALKABLE | cell::NON_WALKABLE_DURING_FIGHT;
        let ground = Ground::of(&map);
        assert!(!ground.walkable(30));
        assert!(!ground.walkable(31), "closed out of fights");
        assert!(ground.walkable(32), "closed only in fights");
        assert!(!ground.walkable(560));
        assert_eq!(ground.step(29, 0), None, "onto an obstacle");
        assert_eq!(ground.step(33, 0), Some(34));
    }

    #[test]
    fn never_passes_between_two_cells_it_cannot_walk() {
        let mut map = open_map();
        // East of 30 is 31: the step passes between its north-east 16 and its south-east 44.
        map.flags[16] = 0;
        map.flags[44] = 0;
        let ground = Ground::of(&map);
        assert_eq!(ground.step(30, 0), None);
        map.flags[44] = cell::WALKABLE;
        assert_eq!(Ground::of(&map).step(30, 0), Some(31), "one side open is enough");
        assert_eq!(ground.step(30, 1), None, "a diagonal onto an obstacle");
    }

    #[test]
    fn counts_floors_only_on_a_map_with_stairs() {
        let mut map = open_map();
        map.floor.insert(31, 170);
        assert_eq!(Ground::of(&map).step(30, 0), Some(31), "no stair: a step of 170 walked (D86)");

        // A stair 31, 32 of moveZone 1; elsewhere moveZone 0.
        map.move_zone.insert(31, 1);
        map.move_zone.insert(32, 1);
        map.floor.insert(31, 30);
        map.floor.insert(32, 60);
        map.floor.insert(33, 10);
        let ground = Ground::of(&map);
        assert_eq!(ground.step(31, 0), Some(32), "30 along the stair");
        assert_eq!(ground.step(30, 0), None, "30 onto the stair");
        assert_eq!(ground.step(32, 0), None, "50 off it");
        assert_eq!(ground.step(34, 4), Some(33), "10 between cells of moveZone 0");
        map.floor.insert(34, 30);
        assert_eq!(Ground::of(&map).step(34, 4), None, "20 between cells of moveZone 0");
    }
}
