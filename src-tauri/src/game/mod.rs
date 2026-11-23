//! The game: what D3B knows of each character played through the relay, built from its connection's decoded messages
//! (`docs/game.md`). It reads the protocol's messages; the protocol knows nothing of it.

mod around;

mod character;

mod life;

mod live;

#[cfg_attr(not(test), expect(dead_code, reason = "the acts of step 4's PR F10 walk the paths"))]
mod path;

pub(crate) use around::{ActorKind, Monster, Seen};
pub(crate) use character::Who;
#[cfg(test)]
pub(crate) use character::{Character, Fights, Pods};
pub(crate) use character::{Context, Sex, View};
#[cfg(test)]
pub(crate) use life::Characteristics;
#[cfg(test)]
pub(crate) use life::LifeNow;
pub(crate) use live::Live;

#[cfg(test)]
mod replay;
