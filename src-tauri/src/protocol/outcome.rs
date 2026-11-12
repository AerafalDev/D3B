//! What reading a frame gave (`docs/protocol.md`, "Decoding"): a decoded message is trusted only when its re-encoding
//! carries the frame's own fields. prost writes a oneof at its lowest number's place and drops the fields our `.proto`
//! lacks, where the client writes fields in number order, so the two are compared field by field in number order.

use crate::relay::wire::{self, Field, Value};

/// What reading one frame gave.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(crate) enum Outcome {
    /// Decoded, and its re-encoding holds the same fields with the same bytes.
    Understood,
    /// Decoded, the same but for fields our `.proto` lacks, present in the frame only.
    Read,
    /// A name without a pin, a secret, or an envelope not as measured: passed on, never decoded.
    #[cfg_attr(
        test,
        expect(
            dead_code,
            reason = "the decoder of a build's frames gives it, in the stack's next PR"
        )
    )]
    Unknown,
    /// A pinned name whose bytes do not decode, or a declared field that re-encodes otherwise.
    NotTrusted,
}

/// How many levels of nested messages are compared; the live view's deepest is 5 levels down. It bounds what a frame
/// can cost.
const DEPTH: usize = 16;

/// How `encoded`, a decoded message encoded again, compares with `frame`, the bytes it was decoded from: `Understood`,
/// `Read` or `NotTrusted`.
pub(crate) fn compare(frame: &[u8], encoded: &[u8]) -> Outcome {
    compared(frame, encoded, DEPTH)
}

fn compared(frame: &[u8], encoded: &[u8], depth: usize) -> Outcome {
    if frame == encoded {
        return Outcome::Understood;
    }
    let (Some(mut sent), Some(mut again)) = (sorted(frame), sorted(encoded)) else {
        return Outcome::NotTrusted;
    };
    // Stable, so that a repeated field's values keep their order.
    sent.sort_by_key(|field| field.number);
    again.sort_by_key(|field| field.number);
    let mut outcome = Outcome::Understood;
    let mut again = again.as_slice();
    for group in sent.chunk_by(|left, right| left.number == right.number) {
        let number = group[0].number;
        let same = again.iter().take_while(|field| field.number == number).count();
        if same == 0 {
            outcome = Outcome::Read;
            continue;
        }
        if same != group.len() {
            return Outcome::NotTrusted;
        }
        for (sent, again) in group.iter().zip(&again[..same]) {
            match field_outcome(sent, again, depth) {
                Outcome::Understood => {}
                Outcome::Read => outcome = Outcome::Read,
                _ => return Outcome::NotTrusted,
            }
        }
        again = &again[same..];
    }
    // A field left is one prost wrote and the frame does not hold: it stopped the matching where it stood.
    if again.is_empty() { outcome } else { Outcome::NotTrusted }
}

/// Two values of one field: equal, or two nested messages compared in turn. prost writes a scalar, a string or packed
/// numbers as it read them, so differing bytes can only be a message.
fn field_outcome(sent: &Field<'_>, again: &Field<'_>, depth: usize) -> Outcome {
    match (sent.value, again.value) {
        (left, right) if left == right => Outcome::Understood,
        (Value::Bytes(left), Value::Bytes(right)) if depth > 0 => compared(left, right, depth - 1),
        _ => Outcome::NotTrusted,
    }
}

fn sorted(message: &[u8]) -> Option<Vec<Field<'_>>> {
    wire::fields(message).collect::<Result<_, _>>().ok()
}

#[cfg(test)]
mod tests {
    use super::*;

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

    #[test]
    fn the_same_bytes_are_understood() {
        let frame = message(&[varint(1, 7), bytes(2, b"Lyra")]);

        assert_eq!(compare(&frame, &frame), Outcome::Understood);
    }

    #[test]
    fn the_same_fields_in_another_order_are_understood() {
        let value = message(&[varint(2, 55)]);
        // The client writes in number order; prost writes the oneof member 3 at the place of its lowest number, 1.
        let frame = message(&[varint(2, 6), bytes(3, &value)]);
        let encoded = message(&[bytes(3, &value), varint(2, 6)]);
        let outer = |inner: &[u8]| message(&[bytes(1, inner), varint(4, 9)]);

        assert_eq!(compare(&frame, &encoded), Outcome::Understood);
        assert_eq!(compare(&outer(&frame), &outer(&encoded)), Outcome::Understood);
    }

    #[test]
    fn fields_the_proto_lacks_make_it_read() {
        let frame = message(&[varint(1, 7), varint(13, 1)]);
        let nested = message(&[
            bytes(2, &message(&[varint(3, 300), varint(4, 3), varint(5, 9)])),
            varint(6, 1),
        ]);
        let nested_again = message(&[bytes(2, &message(&[varint(3, 300), varint(4, 3)])), varint(6, 1)]);

        assert_eq!(compare(&frame, &message(&[varint(1, 7)])), Outcome::Read);
        assert_eq!(compare(&frame, &[]), Outcome::Read);
        assert_eq!(compare(&nested, &nested_again), Outcome::Read);
    }

    #[test]
    fn a_field_that_comes_back_otherwise_is_not_trusted() {
        let frame = message(&[varint(1, 7), varint(2, 8)]);
        for encoded in [
            // Another value.
            message(&[varint(1, 7), varint(2, 9)]),
            // A field the frame does not hold, before its fields, among them and after them.
            message(&[varint(0x7f, 1)]),
            message(&[varint(1, 7), bytes(1, b"x"), varint(2, 8)]),
            message(&[varint(1, 7), varint(2, 8), varint(3, 0)]),
            // Another wire type.
            message(&[varint(1, 7), bytes(2, &[0x08])]),
        ] {
            assert_eq!(compare(&frame, &encoded), Outcome::NotTrusted, "{encoded:?}");
        }
        assert_eq!(
            compare(&message(&[varint(3, 1)]), &message(&[varint(1, 7), varint(3, 1)])),
            Outcome::NotTrusted
        );
    }

    #[test]
    fn a_repeated_field_keeps_its_count_and_its_order() {
        let frame = message(&[varint(1, 5), varint(1, 6)]);

        assert_eq!(
            compare(&frame, &message(&[varint(1, 6), varint(1, 5)])),
            Outcome::NotTrusted
        );
        assert_eq!(compare(&frame, &message(&[varint(1, 5)])), Outcome::NotTrusted);
        assert_eq!(
            compare(&frame, &message(&[varint(1, 5), varint(1, 6), varint(1, 7)])),
            Outcome::NotTrusted
        );
    }

    #[test]
    fn bytes_that_are_not_fields_are_not_trusted() {
        let frame = message(&[bytes(2, &[0xff, 0xff])]);

        assert_eq!(compare(&[0xff], &[]), Outcome::NotTrusted);
        assert_eq!(compare(&frame, &message(&[bytes(2, &[0x08])])), Outcome::NotTrusted);
    }

    #[test]
    fn compares_down_to_its_depth_and_no_further() {
        let nested = |levels: usize, leaf: &[Field<'_>]| {
            let mut inner = message(leaf);
            for _ in 0..levels {
                inner = message(&[bytes(1, &inner)]);
            }
            inner
        };
        let deep = |levels| {
            (
                nested(levels, &[varint(1, 1), varint(2, 2)]),
                nested(levels, &[varint(1, 1)]),
            )
        };

        let (frame, encoded) = deep(DEPTH);
        assert_eq!(compare(&frame, &encoded), Outcome::Read);
        let (frame, encoded) = deep(DEPTH + 1);
        assert_eq!(compare(&frame, &encoded), Outcome::NotTrusted);
    }
}
