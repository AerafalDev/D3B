//! The messages the live view reads (`docs/protocol.md`, "Decoding"), decoded by their readable names.

use prost::Message as _;

use super::game;

/// Declares [`Message`], one variant per readable message, and [`READABLE`], their names: a name pinned to a build
/// must be one of them.
macro_rules! messages {
    ($($name:ident),* $(,)?) => {
        /// A message of the live view, decoded.
        #[derive(Debug, Clone, PartialEq)]
        pub(crate) enum Message {
            $($name(game::$name),)*
        }

        /// The readable names of [`Message`]'s variants.
        pub(crate) const READABLE: &[&str] = &[$(stringify!($name)),*];

        impl Message {
            /// The message of `readable` decoded from `bytes`; `None` when no message bears that name, an error when
            /// the bytes are not that message.
            pub(crate) fn decode(readable: &str, bytes: &[u8]) -> Option<Result<Self, prost::DecodeError>> {
                match readable {
                    $(stringify!($name) => Some(game::$name::decode(bytes).map(Self::$name)),)*
                    _ => None,
                }
            }

            /// The message encoded as prost writes it: the fields our `.proto` declares, in number order, a oneof at
            /// the place of its lowest number whichever member is set.
            pub(crate) fn encode(&self) -> Vec<u8> {
                match self {
                    $(Self::$name(message) => message.encode_to_vec(),)*
                }
            }

            /// Its readable name.
            pub(crate) fn readable(&self) -> &'static str {
                match self {
                    $(Self::$name(_) => stringify!($name),)*
                }
            }
        }
    };
}

messages! {
    CharacterSelectionEvent,
    ContextCreationEvent,
    ContextDestroyEvent,
    MapCurrentEvent,
    MapInformationRequest,
    MapInformationEvent,
    MapActorShownEvent,
    MapActorRemovedEvent,
    MapMovementEvent,
    InteractiveElementUpdatedEvent,
    StatedElementUpdatedEvent,
    CharacterStatsEvent,
    LifeRegenStartEvent,
    LifeRegenEndEvent,
    InventoryWeightEvent,
}

#[cfg(test)]
mod tests;
