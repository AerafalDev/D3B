//! The quickest walk from a cell to the nearest of others on one map, out of fights.

use std::cmp::Reverse;
use std::collections::BinaryHeap;

use crate::data::CELL_COUNT;

use super::grid::{self, DIRECTIONS};
use super::ground::Ground;

/// Why no walk was found. Each is met in normal use: the act or the kind reading it decides what follows.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum PathError {
    #[error("cell {0} is not one of a map's 560")]
    NoSuchCell(u16),
    /// The character stands on a cell asked: a walk of no step is never sent, the server once closed the connection
    /// for one (inferred, `main` D89).
    #[error("the character already stands on cell {0}, a walk would have no step")]
    AlreadyThere(u16),
    #[error("no walk from cell {from} reaches the cells asked")]
    NoWay { from: u16 },
}

/// A walk of at least one step: its cells in order, each a neighbour of the one before.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct CellPath {
    cells: Vec<u16>,
}

/// A turning point of a walk: a cell, and the direction the walk leaves it in, or arrives on it in for the last.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Turn {
    pub cell: u16,
    pub direction: u8,
}

impl Turn {
    /// The turn as the walk request carries it, `direction << 12 | cell` (verified on 3.7.13.17, study `map-walk`: all
    /// 97,976 requests decode so).
    pub(crate) fn key(self) -> i32 {
        i32::from(self.direction) << 12 | i32::from(self.cell)
    }
}

impl CellPath {
    /// The walk's cells, its start first.
    pub(crate) fn cells(&self) -> &[u16] {
        &self.cells
    }

    pub(crate) fn start(&self) -> u16 {
        self.cells[0]
    }

    pub(crate) fn end(&self) -> u16 {
        self.cells[self.cells.len() - 1]
    }

    /// The walk's turning points: its start, each cell where it changes direction, and its end with the direction it
    /// arrives in, as the client's requests list them (verified, study `map-walk`: 95,025 of the 95,229 walks shown).
    pub(crate) fn turns(&self) -> Vec<Turn> {
        let directions: Vec<u8> = self
            .cells
            .windows(2)
            .filter_map(|pair| grid::direction(pair[0], pair[1]))
            .collect();
        let mut turns = vec![Turn {
            cell: self.cells[0],
            direction: directions[0],
        }];
        for (index, pair) in directions.windows(2).enumerate() {
            if pair[0] != pair[1] {
                turns.push(Turn {
                    cell: self.cells[index + 1],
                    direction: pair[1],
                });
            }
        }
        turns.push(Turn {
            cell: self.end(),
            direction: directions[directions.len() - 1],
        });
        turns
    }

    /// The walk cut before its last step, and that last step, when it has two steps or more.
    pub(crate) fn split_last(&self) -> Option<(Self, Self)> {
        let before = self.cells.len().checked_sub(2).filter(|before| *before > 0)?;
        Some((
            Self {
                cells: self.cells[..=before].to_vec(),
            },
            Self {
                cells: self.cells[before..].to_vec(),
            },
        ))
    }
}

/// How long a step takes, for the quickest walk: running, about 170 ms east or west, 110 north or south, 113 along a
/// diagonal (inferred, `main` D68, measured on 3.7.12.12; 3.7.13.17's 209, 126 and 141 ms keep the order, study
/// `map-walk`, section 2).
fn cost(direction: u8) -> u32 {
    match direction {
        0 | 4 => 3,
        _ => 2,
    }
}

/// The quickest walk from `from`, which the character stands on, walkable or not, to the nearest of `goals`.
///
/// A goal that cannot be walked on is no goal. `avoid` lists cells the walk goes round when it can, a map's action
/// cells, which move the character on a walk that only crosses them; it steps on one when no other way exists, or when
/// it is a goal (inferred, `main` D488, measured on 3.7.14.18).
pub(crate) fn find(ground: &Ground, from: u16, goals: &[u16], avoid: &[u16]) -> Result<CellPath, PathError> {
    if let Some(&cell) = goals.iter().chain(avoid).chain([&from]).find(|cell| !grid::exists(**cell)) {
        return Err(PathError::NoSuchCell(cell));
    }
    if goals.contains(&from) {
        return Err(PathError::AlreadyThere(from));
    }
    let mut goal = vec![false; CELL_COUNT];
    for &cell in goals.iter().filter(|cell| ground.walkable(**cell)) {
        goal[usize::from(cell)] = true;
    }
    let mut blocked = vec![false; CELL_COUNT];
    for &cell in avoid.iter().filter(|cell| !goal[usize::from(**cell)]) {
        blocked[usize::from(cell)] = true;
    }
    search(ground, from, &goal, &blocked)
        .or_else(|| search(ground, from, &goal, &vec![false; CELL_COUNT]))
        .ok_or(PathError::NoWay { from })
}

/// Dijkstra over the 560 cells; ties go to the first direction tried, so the same map gives the same walk.
fn search(ground: &Ground, from: u16, goal: &[bool], blocked: &[bool]) -> Option<CellPath> {
    let mut costs = vec![u32::MAX; CELL_COUNT];
    let mut previous = vec![from; CELL_COUNT];
    let mut open = BinaryHeap::new();
    costs[usize::from(from)] = 0;
    open.push(Reverse((0, from)));
    while let Some(Reverse((spent, cell))) = open.pop() {
        if spent > costs[usize::from(cell)] {
            continue;
        }
        if goal[usize::from(cell)] {
            let mut cells = vec![cell];
            while cells[cells.len() - 1] != from {
                cells.push(previous[usize::from(cells[cells.len() - 1])]);
            }
            cells.reverse();
            return Some(CellPath { cells });
        }
        for direction in 0..DIRECTIONS {
            let Some(next) = ground.step(cell, direction).filter(|next| !blocked[usize::from(*next)]) else {
                continue;
            };
            let reached = spent + cost(direction);
            if reached < costs[usize::from(next)] {
                costs[usize::from(next)] = reached;
                previous[usize::from(next)] = cell;
                open.push(Reverse((reached, next)));
            }
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::path::ground::tests::open_map;

    fn open() -> Ground {
        Ground::of(&open_map())
    }

    #[test]
    fn walks_straight_to_a_cell_of_its_row() {
        let path = find(&open(), 30, &[33], &[]).unwrap_or_else(|error| panic!("{error}"));
        // East costs 3, a diagonal 2: two diagonals beat one step east, so 30 to 32 zig-zags.
        assert_eq!((path.start(), path.end()), (30, 33));
        assert!(path.cells().len() >= 4);
    }

    #[test]
    fn goes_south_two_rows_a_step() {
        let path = find(&open(), 30, &[86], &[]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(path.cells(), [30, 58, 86]);
        assert_eq!(
            path.turns(),
            [Turn { cell: 30, direction: 2 }, Turn { cell: 86, direction: 2 }]
        );
        assert_eq!(path.turns()[0].key(), 2 << 12 | 30);
    }

    #[test]
    fn lists_a_turn_where_the_walk_changes_direction() {
        let path = find(&open(), 30, &[59], &[]).unwrap_or_else(|error| panic!("{error}"));
        let turns = path.turns();
        assert_eq!(turns.first().map(|turn| turn.cell), Some(30));
        assert_eq!(turns.last().map(|turn| turn.cell), Some(59));
    }

    #[test]
    fn never_walks_no_step() {
        assert_eq!(find(&open(), 30, &[31, 30], &[]), Err(PathError::AlreadyThere(30)));
    }

    #[test]
    fn refuses_a_cell_off_the_map() {
        assert_eq!(find(&open(), 30, &[560], &[]), Err(PathError::NoSuchCell(560)));
        assert_eq!(find(&open(), 600, &[31], &[]), Err(PathError::NoSuchCell(600)));
        assert_eq!(find(&open(), 30, &[31], &[999]), Err(PathError::NoSuchCell(999)));
    }

    #[test]
    fn says_when_no_walk_reaches_the_goals() {
        let mut map = open_map();
        for neighbour in (0..DIRECTIONS).filter_map(|d| grid::neighbour(300, d)) {
            map.flags[usize::from(neighbour)] = 0;
        }
        assert_eq!(find(&Ground::of(&map), 30, &[300], &[]), Err(PathError::NoWay { from: 30 }));
        map.flags[100] = 0;
        assert_eq!(
            find(&Ground::of(&map), 30, &[100], &[]),
            Err(PathError::NoWay { from: 30 }),
            "an obstacle is no goal"
        );
    }

    #[test]
    fn starts_from_a_cell_it_cannot_walk_on() {
        let mut map = open_map();
        map.flags[30] = 0;
        assert!(find(&Ground::of(&map), 30, &[86], &[]).is_ok());
    }

    #[test]
    fn goes_round_a_map_action_cell_unless_it_is_the_goal() {
        let path = find(&open(), 30, &[86], &[58]).unwrap_or_else(|error| panic!("{error}"));
        assert!(!path.cells().contains(&58), "{:?}", path.cells());
        let onto = find(&open(), 30, &[58], &[58]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(onto.cells(), [30, 58]);
    }

    #[test]
    fn crosses_a_map_action_cell_when_no_other_way_exists() {
        let mut map = open_map();
        // A line of diagonals south-east, the only walkable cells: no way round 59.
        map.flags.fill(0);
        for cell in [30, 44, 59, 73, 88] {
            map.flags[cell] = crate::data::cell::WALKABLE;
        }
        let path = find(&Ground::of(&map), 30, &[88], &[59]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(path.cells(), [30, 44, 59, 73, 88]);
    }

    #[test]
    fn splits_a_walk_before_its_last_step() {
        let path = find(&open(), 30, &[114], &[]).unwrap_or_else(|error| panic!("{error}"));
        let (first, last) = path.split_last().unwrap_or_else(|| panic!("three steps"));
        assert_eq!(first.cells(), [30, 58, 86]);
        assert_eq!(last.cells(), [86, 114]);
        let one = find(&open(), 30, &[58], &[]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(one.split_last(), None);
    }
}
