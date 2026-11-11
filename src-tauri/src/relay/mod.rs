//! The relay between the official client and Ankama's servers, on the same machine (`docs/relay.md`, ADR 0011).
//!
//! At start it points the client's `ConfigUrl` at a copy of Ankama's configuration whose connection host is the relay
//! on loopback, and puts the line back at quit. Every frame passes through untouched but the server-selection answer,
//! whose game host and ports become the relay's. Nothing is sent to the game of the relay's own.

mod ankama_config;
mod connection_phase;
mod error;
mod frame;
mod handover;
mod redirect;
mod version_file;
mod wire;

pub use error::RelayError;

use std::net::Ipv4Addr;

/// The loopback address the relay listens on: not `127.0.0.1`, so that no other local program's port is taken
/// (inferred, `main:docs/client.md`, D34 and D36, on Windows x64).
pub(crate) const ADDRESS: Ipv4Addr = Ipv4Addr::new(127, 0, 13, 59);

/// The port of the relay's game listener, which the server-selection answer names in place of Ankama's (inferred,
/// `main:docs/client-game.md`, "The game connection", measured on Windows x64).
pub(crate) const GAME_PORT: u16 = 5556;
