//! The game: what D3B knows of each character played through the relay, built from its connection's decoded messages
//! (`docs/game.md`). It reads the protocol's messages; the protocol knows nothing of it.

mod around;

mod character;

mod life;

#[cfg(test)]
mod replay;
