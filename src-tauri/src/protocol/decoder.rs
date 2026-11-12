//! A game frame read with its build's pins (`docs/protocol.md`, "Decoding"): its envelope opened, a secret message
//! left undecoded, a pinned one decoded and compared with its frame, which gives its outcome.

use super::envelope;
use super::message::Message;
use super::outcome::{self, Outcome};
use super::pins::Pins;
use crate::relay::Side;

/// What one frame `side` sent gives with `pins`: its outcome, and its message when it was understood or read.
pub(crate) fn read(pins: &Pins, side: Side, body: &[u8]) -> (Outcome, Option<Message>) {
    let Some(content) = envelope::open(side, body) else {
        return (Outcome::Unknown, None);
    };
    // A secret is never pinned (`Pins::parse`): its bytes are never decoded, so no session secret is held as a message.
    let Some(readable) = pins.readable(content.name) else {
        return (Outcome::Unknown, None);
    };
    match Message::decode(readable, content.value) {
        Some(Ok(message)) => match outcome::compare(content.value, &message.encode()) {
            Outcome::NotTrusted => (Outcome::NotTrusted, None),
            trusted => (trusted, Some(message)),
        },
        // A pinned name is one of ours (`Pins::parse`): a message that does not decode is all that is left.
        Some(Err(_)) | None => (Outcome::NotTrusted, None),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::game;
    use crate::relay::wire::{self, Field, Value};

    fn message(fields: &[Field<'_>]) -> Vec<u8> {
        let mut out = Vec::new();
        wire::encode(fields, &mut out);
        out
    }

    fn varint(number: u32, value: u64) -> Field<'static> {
        Field {
            number,
            value: Value::Varint(value),
        }
    }

    fn bytes(number: u32, value: &[u8]) -> Field<'_> {
        Field {
            number,
            value: Value::Bytes(value),
        }
    }

    /// A server event of the message `name`, holding `value`.
    fn event(name: &str, value: &[u8]) -> Vec<u8> {
        let url = format!("type.ankama.com/{name}");
        let any = message(&[bytes(1, url.as_bytes()), bytes(2, value)]);
        message(&[bytes(3, &message(&[bytes(1, &any)]))])
    }

    fn pins() -> Pins {
        Pins::embedded("3.7.13.17")
            .and_then(Result::ok)
            .unwrap_or_else(|| panic!("no pins of 3.7.13.17"))
    }

    #[test]
    fn a_pinned_message_whole_is_understood() {
        let frame = event("jow", &message(&[varint(1, 153_092_354)]));

        assert_eq!(
            read(&pins(), Side::Server, &frame),
            (
                Outcome::Understood,
                Some(Message::MapCurrentEvent(game::MapCurrentEvent { map_id: 153_092_354 }))
            )
        );
    }

    #[test]
    fn a_pinned_message_with_fields_we_lack_is_read() {
        let frame = event("iqq", &message(&[varint(1, 1_200), varint(2, 3_000), varint(3, 1)]));

        let (outcome, message) = read(&pins(), Side::Server, &frame);

        assert_eq!(outcome, Outcome::Read);
        assert_eq!(
            message,
            Some(Message::InventoryWeightEvent(game::InventoryWeightEvent {
                weight: 1_200,
                max_weight: 3_000
            }))
        );
    }

    #[test]
    fn a_name_not_pinned_a_secret_or_another_envelope_is_unknown() {
        let value = message(&[bytes(1, b"0123456789abcdef0123456789abcdef"), bytes(2, b"fr")]);
        let any = message(&[bytes(1, b"type.ankama.com/knc"), bytes(2, &value)]);
        let ticket = message(&[bytes(4, &message(&[varint(1, u64::MAX), bytes(2, &any)]))]);
        for (side, frame) in [
            (Side::Server, event("jqk", &[])),
            (Side::Client, ticket),
            (Side::Client, event("jow", &message(&[varint(1, 1)]))),
            (Side::Server, vec![0xff]),
        ] {
            assert_eq!(read(&pins(), side, &frame), (Outcome::Unknown, None), "{side:?}");
        }
    }

    #[test]
    fn a_pinned_message_that_does_not_decode_or_comes_back_otherwise_is_not_trusted() {
        for value in [
            // Field 1, the map, written as bytes where it is a number.
            message(&[bytes(1, b"x")]),
            // Field 1 twice: prost keeps the last, and its encoding holds one.
            message(&[varint(1, 1), varint(1, 2)]),
        ] {
            assert_eq!(
                read(&pins(), Side::Server, &event("jow", &value)),
                (Outcome::NotTrusted, None)
            );
        }
    }
}
