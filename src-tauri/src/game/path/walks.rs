//! The walks that take the character to the nearest of some cells, none ending where its last walk ended.
//!
//! The client did not follow 36 of the 39 walks D3B sent ending on the cell the character's previous walk ended on,
//! even on another map, against 9 of 695 others (inferred, `main` D260, measured on 3.7.12.12; study `map-walk`,
//! section 4: 11 of 16 on 3.7.13.17). Why the client keeps that cell is not known.

use super::cell_path::{CellPath, PathError, find};
use super::grid::DIRECTIONS;
use super::ground::Ground;

/// The walks, in order, from `from` to the nearest of `goals`, going round `avoid` as [`find`] does.
///
/// `last_end` is the cell the character's last walk the server showed ended on, whatever the map. A goal other than it
/// is taken when one is reached, as a map's other exit cells; a walk that must still end there goes in two: to its
/// cell before the last, then the last step, or a step aside first when it has one step (`main` D260). With no step
/// aside, the one walk is given: the act's deadline then meets the client's silence.
pub(crate) fn walks(
    ground: &Ground,
    from: u16,
    goals: &[u16],
    avoid: &[u16],
    last_end: Option<u16>,
) -> Result<Vec<CellPath>, PathError> {
    let Some(last_end) = last_end else {
        return find(ground, from, goals, avoid).map(|path| vec![path]);
    };
    let others: Vec<u16> = goals.iter().copied().filter(|goal| *goal != last_end).collect();
    if !others.is_empty() && !goals.contains(&from) {
        if let Ok(path) = find(ground, from, &others, avoid) {
            return Ok(vec![path]);
        }
    }
    let path = find(ground, from, goals, avoid)?;
    if path.end() != last_end {
        return Ok(vec![path]);
    }
    if let Some((first, last)) = path.split_last() {
        return Ok(vec![first, last]);
    }
    Ok(aside(ground, from, path.end(), avoid).unwrap_or_else(|| vec![path]))
}

/// A step from `from` to a neighbour other than `to`, then the walk from there to `to`.
fn aside(ground: &Ground, from: u16, to: u16, avoid: &[u16]) -> Option<Vec<CellPath>> {
    let ordered = (0..DIRECTIONS).filter_map(|direction| ground.step(from, direction));
    let (clear, avoided): (Vec<u16>, Vec<u16>) = ordered.filter(|cell| *cell != to).partition(|cell| !avoid.contains(cell));
    clear.into_iter().chain(avoided).find_map(|side| {
        let step = find(ground, from, &[side], &[]).ok()?;
        let back = find(ground, side, &[to], avoid).ok()?;
        Some(vec![step, back])
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::path::ground::tests::open_map;

    fn open() -> Ground {
        Ground::of(&open_map())
    }

    fn ends(walks: &[CellPath]) -> Vec<(u16, u16)> {
        walks.iter().map(|walk| (walk.start(), walk.end())).collect()
    }

    #[test]
    fn walks_once_when_the_last_walk_ended_elsewhere() {
        let walks = walks(&open(), 30, &[114], &[], Some(31)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(ends(&walks), [(30, 114)]);
        let first = super::walks(&open(), 30, &[114], &[], None).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(ends(&first), [(30, 114)]);
    }

    #[test]
    fn leaves_by_another_goal_than_where_the_last_walk_ended() {
        // Two exit cells; the nearer is where the last walk ended.
        let walks = walks(&open(), 30, &[86, 300], &[], Some(86)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(ends(&walks), [(30, 300)]);
    }

    #[test]
    fn splits_a_walk_that_must_end_where_the_last_one_ended() {
        let walks = walks(&open(), 30, &[114], &[], Some(114)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(ends(&walks), [(30, 86), (86, 114)]);
    }

    #[test]
    fn steps_aside_before_a_walk_of_one_step() {
        let walks = walks(&open(), 30, &[58], &[], Some(58)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(walks.len(), 2);
        assert_eq!(walks[0].start(), 30);
        assert_ne!(walks[0].end(), 58);
        assert_eq!((walks[1].start(), walks[1].end()), (walks[0].end(), 58));
    }

    #[test]
    fn walks_once_when_no_step_aside_exists() {
        let mut map = open_map();
        map.flags.fill(0);
        map.flags[44] = crate::data::cell::WALKABLE;
        let walks = walks(&Ground::of(&map), 30, &[44], &[], Some(44)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(ends(&walks), [(30, 44)]);
    }

    #[test]
    fn never_walks_no_step_even_where_the_last_walk_ended() {
        assert_eq!(walks(&open(), 30, &[30], &[], Some(30)), Err(PathError::AlreadyThere(30)));
        assert_eq!(walks(&open(), 30, &[30, 86], &[], Some(86)), Err(PathError::AlreadyThere(30)));
    }
}
