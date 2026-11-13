//! The messages the live view reads (`docs/protocol.md`, "Decoding"), decoded by their readable names.

use prost::Message as _;

use super::game;

/// Declares [`Message`], one variant per readable message: a name pinned to a build must be one of them.
macro_rules! messages {
    ($($name:ident),* $(,)?) => {
        /// A message of the live view, decoded.
        #[derive(Debug, Clone, PartialEq)]
        pub(crate) enum Message {
            $($name(game::$name),)*
        }

        /// The readable names of [`Message`]'s variants: those a build's pins may pin.
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

            /// Its fields by their names, a line each, nested ones indented under their field, without its own
            /// name: the inspector's list names the message already.
            pub(crate) fn fields(&self) -> Vec<String> {
                let text = match self {
                    $(Self::$name(message) => format!("{message:#?}"),)*
                };
                fields_of(&text)
            }

            /// The message encoded as prost writes it: the fields our `.proto` declares, in number order, a oneof at
            /// the place of its lowest number whichever member is set.
            pub(crate) fn encode(&self) -> Vec<u8> {
                match self {
                    $(Self::$name(message) => message.encode_to_vec(),)*
                }
            }
        }
    };
}

/// The lines inside a message's pretty `Debug` text: its first line (its name and a brace) and its last (the brace)
/// left out, one level of indentation taken off, and the commas that end fields dropped. A message without fields is
/// its name alone, and has none.
fn fields_of(text: &str) -> Vec<String> {
    let lines: Vec<&str> = text.lines().collect();
    if lines.len() < 3 {
        return Vec::new();
    }
    lines[1..lines.len() - 1]
        .iter()
        .map(|line| {
            let line = line.strip_prefix("    ").unwrap_or(line);
            line.strip_suffix(',').unwrap_or(line).to_owned()
        })
        .collect()
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
    CharacterLevelUpEvent,
    KamasUpdateEvent,
}

#[cfg(test)]
mod tests;
