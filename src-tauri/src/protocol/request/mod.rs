//! The bot's requests (`docs/protocol.md`, "Requests"; ADR 0021), each encoded as the client frames its own: the root's
//! field 4, a request `{ int32 id = 1, Any = 2 }`, its `Any` `{ type_url = 1, value = 2 }` naming the message by the
//! build's pins, the value left out when the message is empty (verified, `protocol::envelope`, 3.7.13.17). The relay
//! writes the frame between the client's (`relay::BotFrames`).
#![cfg_attr(
    not(test),
    expect(dead_code, reason = "the bot's acts send its requests from step 4's R1")
)]

use prost::Message as _;

use super::game;
use super::pins::Pins;
use crate::relay::frame;
use crate::relay::wire::{self, Field, Value};

/// The id of every request the bot sends: -1, as the C# version's and the client's own requests of the walk, the map
/// change and the harvest (inferred, `main:docs/client-game.md`, "The game, acted on", 3.7.12.12; the client numbers
/// only four kinds of its own, from 2). The replay of the owner's captures checks it on the C# bot's frames.
const ID: i64 = -1;

/// The prefix of every type URL (verified, 3.7.13.17).
const TYPE_URL: &str = "type.ankama.com/";

/// Declares [`Request`], one variant per request the bot sends, by its readable name, and [`ENCODED`].
macro_rules! requests {
    ($($name:ident),* $(,)?) => {
        /// A request the bot sends to the game server.
        #[derive(Debug, Clone, PartialEq)]
        pub(crate) enum Request {
            $($name(game::$name),)*
        }

        /// The readable names of [`Request`]'s variants: the requests D3B encodes, the coverage report's column.
        pub(crate) const ENCODED: &[&str] = &[$(stringify!($name)),*];

        impl Request {
            /// Its readable name, which the build's pins pair with the build's.
            fn readable(&self) -> &'static str {
                match self {
                    $(Self::$name(_) => stringify!($name),)*
                }
            }

            /// Its message's bytes, as prost writes them.
            fn value(&self) -> Vec<u8> {
                match self {
                    $(Self::$name(message) => message.encode_to_vec(),)*
                }
            }

            /// The request of `readable` decoded from `bytes`; `None` when the bot sends no request of that name.
            #[cfg(test)]
            pub(crate) fn decode(readable: &str, bytes: &[u8]) -> Option<Result<Self, prost::DecodeError>> {
                match readable {
                    $(stringify!($name) => Some(game::$name::decode(bytes).map(Self::$name)),)*
                    _ => None,
                }
            }
        }
    };
}

requests! {
    MapMovementRequest,
    MapChangeRequest,
}

/// A request that cannot be encoded for a build.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum RequestError {
    #[error("the build's pins do not pin {readable}, so the bot cannot send it")]
    NotPinned { readable: &'static str },
}

impl Request {
    /// The request as one whole frame, its header included, the client's envelope around it with the id -1 and the
    /// name `pins` give it.
    pub(crate) fn frame(&self, pins: &Pins) -> Result<Vec<u8>, RequestError> {
        let readable = self.readable();
        let name = pins.name_of(readable).ok_or(RequestError::NotPinned { readable })?;
        let url = format!("{TYPE_URL}{name}");
        let bytes = self.value();
        let url = Field {
            number: 1,
            value: Value::Bytes(url.as_bytes()),
        };
        let value = Field {
            number: 2,
            value: Value::Bytes(&bytes),
        };
        let fields = [url, value];
        let written = if bytes.is_empty() { &fields[..1] } else { &fields[..] };
        let mut any = Vec::new();
        wire::encode(written, &mut any);
        let mut request = Vec::new();
        wire::encode(
            &[
                Field {
                    number: 1,
                    value: Value::Varint(ID.cast_unsigned()),
                },
                Field {
                    number: 2,
                    value: Value::Bytes(&any),
                },
            ],
            &mut request,
        );
        let mut root = Vec::new();
        wire::encode(
            &[Field {
                number: 4,
                value: Value::Bytes(&request),
            }],
            &mut root,
        );
        let mut out = Vec::new();
        frame::encode(&root, &mut out);
        Ok(out)
    }
}

#[cfg(test)]
mod tests;
