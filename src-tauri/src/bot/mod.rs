//! The bot: what the player tells it, as Unreal-like blueprints that characters run (`docs/bot.md`, ADR 0014). A
//! blueprint is read from its JSON file and written back here (ADR 0015), and checked for the problems that keep it
//! from running, against the catalogue of the nodes it may hold. The player's files are `storage`'s; the runtime comes
//! in the next PRs of step 3's stack (`docs/now.md`).

mod blueprint;
mod catalogue;
mod copy;
mod given;
mod json;
mod pins;
mod problems;
mod variable;
mod wiring;

pub(crate) use blueprint::{Blueprint, Id};
pub(crate) use copy::{Texts, copy, id_for};
pub(crate) use given::given;
#[cfg(test)]
pub(crate) use json::tests::HARVEST;
pub(crate) use json::{ReadError, read, write};
pub(crate) use problems::problems;
