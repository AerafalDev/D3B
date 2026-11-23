//! How the cells of a map lie: 14 columns of 40 staggered rows, and the eight directions between neighbours.
//!
//! A cell is its row times 14 plus its column. Odd rows lie half a cell east of even ones, so a cell has eight
//! neighbours: east and west in its row, south and north two rows away, the four diagonals in the rows next to it.
//! Directions run from 0, east, clockwise to 7, as in the client's data and its walk requests. Verified on 3.7.13.17
//! (study `map-walk`, section 1): of the C# bot's 95,229 walks the server showed, every step was such a neighbour, from
//! an even row the diagonals +14, +13, −15, −14, from an odd one +15, +14, −14, −13.

use crate::data::CELL_COUNT;

/// The number of cells in a row.
pub(crate) const COLUMNS: u16 = 14;

/// The number of rows.
#[expect(clippy::cast_possible_truncation, reason = "560 cells fit a u16")]
pub(crate) const ROWS: u16 = CELL_COUNT as u16 / COLUMNS;

/// The number of directions, from 0 (east) clockwise to 7.
pub(crate) const DIRECTIONS: u8 = 8;

/// Whether `cell` is one of a map's 560.
pub(crate) fn exists(cell: u16) -> bool {
    usize::from(cell) < CELL_COUNT
}

/// The neighbour of `cell` in `direction`, or `None` off the map.
pub(crate) fn neighbour(cell: u16, direction: u8) -> Option<u16> {
    if !exists(cell) {
        return None;
    }
    let row = i32::from(cell / COLUMNS);
    let column = i32::from(cell % COLUMNS);
    let odd = row % 2;
    // In a diagonal's row, an odd row's neighbour is one column further east than an even row's.
    let (columns, rows) = match direction {
        0 => (1, 0),
        1 => (odd, 1),
        2 => (0, 2),
        3 => (odd - 1, 1),
        4 => (-1, 0),
        5 => (odd - 1, -1),
        6 => (0, -2),
        7 => (odd, -1),
        _ => return None,
    };
    let (column, row) = (column + columns, row + rows);
    if !(0..i32::from(COLUMNS)).contains(&column) || !(0..i32::from(ROWS)).contains(&row) {
        return None;
    }
    u16::try_from(row * i32::from(COLUMNS) + column).ok()
}

/// The direction from `from` to its neighbour `to`, or `None` when they are no neighbours.
pub(crate) fn direction(from: u16, to: u16) -> Option<u8> {
    (0..DIRECTIONS).find(|&direction| neighbour(from, direction) == Some(to))
}

/// Whether a step in `direction` passes between two cells: east, south, west and north, as the screen shows them.
pub(crate) fn is_across(direction: u8) -> bool {
    direction.is_multiple_of(2)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn gives_the_neighbours_of_an_even_and_an_odd_row() {
        // Cell 30 is row 2 (even), cell 44 row 3 (odd).
        let even: Vec<_> = (0..DIRECTIONS).map(|d| neighbour(30, d)).collect();
        let odd: Vec<_> = (0..DIRECTIONS).map(|d| neighbour(44, d)).collect();
        assert_eq!(
            even,
            [31, 44, 58, 43, 29, 15, 2, 16].map(Some).to_vec(),
            "east, then clockwise"
        );
        assert_eq!(odd, [45, 59, 72, 58, 43, 30, 16, 31].map(Some).to_vec());
    }

    #[test]
    fn has_no_neighbour_off_the_map() {
        assert_eq!(neighbour(0, 4), None, "west of the first cell");
        assert_eq!(neighbour(0, 5), None);
        assert_eq!(neighbour(0, 6), None);
        assert_eq!(neighbour(13, 0), None, "east of the first row's last");
        assert_eq!(neighbour(27, 1), None, "an odd row's last column has no south-east");
        assert_eq!(neighbour(559, 2), None, "south of the last cell");
        assert_eq!(neighbour(560, 0), None, "no such cell");
        assert_eq!(neighbour(30, 8), None, "no such direction");
    }

    #[test]
    fn finds_the_direction_between_neighbours_only() {
        assert_eq!(direction(30, 44), Some(1));
        assert_eq!(direction(44, 30), Some(5));
        assert_eq!(direction(30, 32), None);
        assert!(is_across(0) && is_across(6) && !is_across(1));
    }

    #[test]
    fn steps_back_by_the_opposite_direction() {
        for cell in 0..560 {
            for d in 0..DIRECTIONS {
                if let Some(next) = neighbour(cell, d) {
                    assert_eq!(neighbour(next, (d + 4) % DIRECTIONS), Some(cell), "{cell} {d}");
                }
            }
        }
    }
}
