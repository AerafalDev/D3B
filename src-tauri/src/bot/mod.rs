//! The bot: what the player tells it, as Unreal-like blueprints that characters run (`docs/bot.md`, ADR 0014). A
//! blueprint is read from its JSON file and written back here (ADR 0015), and checked for the problems that keep it
//! from running. The catalogue of nodes, the storage of the player's files and the runtime come in the next PRs of
//! step 3's stack (`docs/now.md`).

mod blueprint;
mod json;
mod problems;
mod variable;
