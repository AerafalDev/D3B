//! The game: what D3B knows of each character played through the relay, built from its connection's decoded messages
//! (`docs/game.md`). It reads the protocol's messages; the protocol knows nothing of it.

mod around;

mod character;

mod life;

mod live;

pub(crate) use around::{ActorKind, Monster, Seen};
pub(crate) use character::Who;
pub(crate) use character::{Context, Sex, View};
#[cfg(test)]
pub(crate) use life::Characteristics;
pub(crate) use live::Live;

#[cfg(test)]
mod replay;
