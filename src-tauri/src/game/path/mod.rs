//! The paths: how the character walks on a map and goes from map to map, pure functions of the game data and of
//! where it stands, with no I/O (`docs/play.md`, ADR 0021). The acts send what they give.

mod cell_path;
mod grid;
mod ground;
mod walks;

pub(crate) use cell_path::{CellPath, PathError, find};
pub(crate) use ground::Ground;
pub(crate) use walks::walks;

#[cfg(test)]
mod tests;
