//! Property tests of the cell paths on generated maps, each checked against rules written again here from the map's
//! data, and against a slow search that cannot be wrong in the same way.

use crate::data::{CELL_COUNT, MapData, cell};

use super::cell_path::{CellPath, PathError, find};
use super::grid::{self, DIRECTIONS};
use super::ground::Ground;
use super::ground::tests::open_map;
use super::walks::walks;

/// `SplitMix64`: a seeded source, so a failing map is found again from its seed.
struct Seeded(u64);

impl Seeded {
    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }

    fn below(&mut self, bound: u64) -> u64 {
        self.next() % bound
    }

    fn cell(&mut self) -> u16 {
        u16::try_from(self.below(CELL_COUNT as u64)).unwrap_or(0)
    }
}

/// A map of obstacles, closed cells, floors in tens and, on one map in two, a stair of moveZone 1.
fn generated(seeded: &mut Seeded) -> MapData {
    let mut map = open_map();
    let open = 55 + seeded.below(40);
    for flags in &mut map.flags {
        *flags = match seeded.below(100) {
            roll if roll < open => cell::WALKABLE,
            roll if roll < open + 2 => cell::WALKABLE | cell::NON_WALKABLE_DURING_ROLEPLAY,
            _ => 0,
        };
    }
    let stairs = seeded.below(2) == 0;
    for index in 0..CELL_COUNT {
        let index = u16::try_from(index).unwrap_or(0);
        let floor = i32::try_from(seeded.below(4)).unwrap_or(0) * 10;
        if floor != 0 {
            map.floor.insert(index, floor);
        }
        if stairs && seeded.below(6) == 0 {
            map.move_zone.insert(index, 1);
            map.floor
                .insert(index, i32::try_from(seeded.below(8)).unwrap_or(0) * 30);
        }
    }
    map
}

fn walkable(map: &MapData, at: u16) -> bool {
    map.flags[usize::from(at)] & (cell::WALKABLE | cell::NON_WALKABLE_DURING_ROLEPLAY) == cell::WALKABLE
}

/// The step's rules again, from the map's data: a walkable neighbour, not between two obstacles, the floors' rule.
fn legal(map: &MapData, from: u16, to: u16) -> Option<u32> {
    let direction = grid::direction(from, to)?;
    if !walkable(map, to) {
        return None;
    }
    if direction.is_multiple_of(2) {
        let sides = [direction + 1, direction + DIRECTIONS - 1]
            .map(|side| grid::neighbour(from, side % DIRECTIONS).is_some_and(|side| walkable(map, side)));
        if !sides.contains(&true) {
            return None;
        }
    }
    let zone = |at: u16| map.move_zone.get(&at).copied().unwrap_or(0);
    let floor = |at: u16| map.floor.get(&at).copied().unwrap_or(0);
    let along_a_stair = zone(from) == 1 && zone(to) == 1;
    if !map.move_zone.is_empty() && !along_a_stair && (floor(from) - floor(to)).abs() > 10 {
        return None;
    }
    Some(if direction == 0 || direction == 4 { 3 } else { 2 })
}

/// The cost of the quickest walk to each cell, by relaxing every step until nothing changes (Bellman-Ford).
fn slow_costs(map: &MapData, from: u16, blocked: &[u16]) -> Vec<Option<u32>> {
    let mut costs = vec![None; CELL_COUNT];
    costs[usize::from(from)] = Some(0);
    let mut changed = true;
    while changed {
        changed = false;
        for at in 0..CELL_COUNT {
            let at = u16::try_from(at).unwrap_or(0);
            let Some(spent) = costs[usize::from(at)] else { continue };
            for next in (0..DIRECTIONS).filter_map(|d| grid::neighbour(at, d)) {
                if blocked.contains(&next) {
                    continue;
                }
                if let Some(step) = legal(map, at, next) {
                    let reached = spent + step;
                    if costs[usize::from(next)].is_none_or(|known| reached < known) {
                        costs[usize::from(next)] = Some(reached);
                        changed = true;
                    }
                }
            }
        }
    }
    costs
}

fn cost_of(map: &MapData, path: &CellPath) -> u32 {
    path.cells()
        .windows(2)
        .map(|pair| legal(map, pair[0], pair[1]).unwrap_or_else(|| panic!("an illegal step {pair:?}")))
        .sum()
}

#[test]
fn every_walk_found_is_legal_quickest_and_of_one_step_at_least() {
    let mut seeded = Seeded(0x0D3B);
    let mut found = 0;
    for _ in 0..150 {
        let map = generated(&mut seeded);
        let ground = Ground::of(&map);
        for _ in 0..6 {
            let from = seeded.cell();
            let goals = [seeded.cell(), seeded.cell()];
            let costs = slow_costs(&map, from, &[]);
            let best = goals
                .iter()
                .filter(|goal| walkable(&map, **goal))
                .filter_map(|goal| costs[usize::from(*goal)])
                .min();
            match find(&ground, from, &goals, &[]) {
                Ok(path) => {
                    found += 1;
                    assert_eq!(path.start(), from);
                    assert!(goals.contains(&path.end()));
                    assert!(path.cells().len() >= 2, "a walk of no step");
                    assert_eq!(Some(cost_of(&map, &path)), best, "not the quickest, map {map:?}");
                }
                Err(PathError::AlreadyThere(cell)) => assert!(cell == from && goals.contains(&from)),
                Err(PathError::NoWay { .. }) => assert_eq!(best, None, "a walk exists from {from} to {goals:?}"),
                Err(PathError::NoSuchCell(cell)) => panic!("cell {cell} exists"),
            }
        }
    }
    assert!(found > 300, "too few walks found to test: {found}");
}

#[test]
fn a_walk_goes_round_the_cells_avoided_when_it_can() {
    let mut seeded = Seeded(0x0488);
    for _ in 0..150 {
        let map = generated(&mut seeded);
        let ground = Ground::of(&map);
        let (from, goal) = (seeded.cell(), seeded.cell());
        let avoid: Vec<u16> = (0..8).map(|_| seeded.cell()).collect();
        let Ok(path) = find(&ground, from, &[goal], &avoid) else {
            continue;
        };
        let blocked: Vec<u16> = avoid.iter().copied().filter(|cell| *cell != goal).collect();
        let round = slow_costs(&map, from, &blocked)[usize::from(goal)];
        let crossed = path.cells()[1..path.cells().len() - 1]
            .iter()
            .any(|cell| avoid.contains(cell));
        assert_eq!(
            crossed,
            round.is_none(),
            "from {from} to {goal}, avoiding {avoid:?}: {:?}",
            path.cells()
        );
        if let Some(round) = round {
            assert_eq!(cost_of(&map, &path), round);
        }
    }
}

#[test]
fn the_turns_give_the_walk_back() {
    let mut seeded = Seeded(0x0012);
    for _ in 0..150 {
        let map = generated(&mut seeded);
        let Ok(path) = find(&Ground::of(&map), seeded.cell(), &[seeded.cell()], &[]) else {
            continue;
        };
        let turns = path.turns();
        let mut cells = vec![turns[0].cell];
        for pair in turns.windows(2) {
            while cells[cells.len() - 1] != pair[1].cell {
                let next = grid::neighbour(cells[cells.len() - 1], pair[0].direction);
                cells.push(next.unwrap_or_else(|| panic!("a turn leads off the map: {turns:?}")));
            }
        }
        assert_eq!(cells, path.cells());
        assert!(
            turns
                .iter()
                .all(|turn| turn.key() >> 12 < 8 && turn.key() & 0xFFF < 560)
        );
    }
}

#[test]
fn walks_chain_and_none_ends_where_the_last_one_ended_unless_no_way_round_exists() {
    let mut seeded = Seeded(0x0260);
    let mut split = 0;
    for _ in 0..150 {
        let map = generated(&mut seeded);
        let ground = Ground::of(&map);
        let from = seeded.cell();
        let goal = seeded.cell();
        let last_end = if seeded.below(2) == 0 { goal } else { seeded.cell() };
        let Ok(walks) = walks(&ground, from, &[goal], &[], Some(last_end)) else {
            continue;
        };
        assert_eq!(walks[0].start(), from);
        assert_eq!(walks[walks.len() - 1].end(), goal);
        for pair in walks.windows(2) {
            assert_eq!(pair[0].end(), pair[1].start());
        }
        assert!(walks.iter().all(|walk| walk.cells().len() >= 2));
        if walks.len() > 1 {
            split += 1;
            assert_ne!(walks[0].end(), last_end);
        } else if walks[0].end() == last_end {
            // Kept whole: one step, and no neighbour to step aside to from which the goal is reached.
            assert_eq!(walks[0].cells().len(), 2);
            let aside = (0..DIRECTIONS)
                .filter_map(|d| ground.step(from, d))
                .filter(|side| *side != goal)
                .any(|side| slow_costs(&map, side, &[])[usize::from(goal)].is_some());
            assert!(!aside, "from {from} to {goal}");
        }
    }
    assert!(split > 20, "too few walks split to test: {split}");
}
