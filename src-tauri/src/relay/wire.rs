//! A protobuf wire-format walker: fields and their raw values, with no schema. The relay needs no more to find a
//! ticket and to rewrite the server-selection answer (`docs/relay.md`); reading messages by their meaning belongs to
//! the protocol.

use std::fmt;

use super::RelayError;

/// One field of a message as the wire holds it.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Field<'a> {
    pub(crate) number: u32,
    pub(crate) value: Value<'a>,
}

/// A field's value by its wire type. Groups (types 3 and 4), which proto3 never writes, are refused. Its `Debug` shows
/// a length-delimited value's length only, since it may be a ticket or a game frame.
#[derive(Clone, Copy, PartialEq, Eq)]
pub(crate) enum Value<'a> {
    Varint(u64),
    Fixed64(u64),
    Bytes(&'a [u8]),
    Fixed32(u32),
}

/// The fields of a message, in order, read as they are asked for, with no allocation; an error at the first byte that
/// is not a field ends them.
#[derive(Debug, Clone)]
pub(crate) struct Fields<'a> {
    rest: &'a [u8],
}

/// The fields of `message`.
pub(crate) fn fields(message: &[u8]) -> Fields<'_> {
    Fields { rest: message }
}

impl<'a> Iterator for Fields<'a> {
    type Item = Result<Field<'a>, RelayError>;

    fn next(&mut self) -> Option<Self::Item> {
        if self.rest.is_empty() {
            return None;
        }
        let field = next_field(self.rest);
        match field {
            Ok((field, rest)) => {
                self.rest = rest;
                Some(Ok(field))
            }
            Err(error) => {
                self.rest = &[];
                Some(Err(error))
            }
        }
    }
}

impl fmt::Debug for Value<'_> {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Varint(value) => write!(formatter, "Varint({value})"),
            Self::Fixed64(value) => write!(formatter, "Fixed64({value})"),
            Self::Bytes(bytes) => write!(formatter, "Bytes(<{} bytes>)", bytes.len()),
            Self::Fixed32(value) => write!(formatter, "Fixed32({value})"),
        }
    }
}

/// The first field of `message` and what follows it.
fn next_field(message: &[u8]) -> Result<(Field<'_>, &[u8]), RelayError> {
    let mut rest = message;
    let (key, used) = read_varint(rest).ok_or(RelayError::Wire)?;
    rest = &rest[used..];
    let number = u32::try_from(key >> 3).map_err(|_| RelayError::Wire)?;
    if number == 0 {
        return Err(RelayError::Wire);
    }
    let value = match key & 7 {
        0 => {
            let (value, used) = read_varint(rest).ok_or(RelayError::Wire)?;
            rest = &rest[used..];
            Value::Varint(value)
        }
        1 => {
            let (bytes, tail) = rest.split_first_chunk::<8>().ok_or(RelayError::Wire)?;
            rest = tail;
            Value::Fixed64(u64::from_le_bytes(*bytes))
        }
        2 => {
            let (length, used) = read_varint(rest).ok_or(RelayError::Wire)?;
            let length = usize::try_from(length).map_err(|_| RelayError::Wire)?;
            let tail = &rest[used..];
            if tail.len() < length {
                return Err(RelayError::Wire);
            }
            rest = &tail[length..];
            Value::Bytes(&tail[..length])
        }
        5 => {
            let (bytes, tail) = rest.split_first_chunk::<4>().ok_or(RelayError::Wire)?;
            rest = tail;
            Value::Fixed32(u32::from_le_bytes(*bytes))
        }
        _ => return Err(RelayError::Wire),
    };
    Ok((Field { number, value }, rest))
}

/// Appends `fields` to `out` in their minimal encoding. A message that encodes back to its own bytes was read whole at
/// its own level: its length-delimited values stay opaque, so a nested message is checked on its own
/// (`docs/relay.md`, "The one rewrite").
pub(crate) fn encode(fields: &[Field<'_>], out: &mut Vec<u8>) {
    for field in fields {
        let wire_type = match field.value {
            Value::Varint(_) => 0,
            Value::Fixed64(_) => 1,
            Value::Bytes(_) => 2,
            Value::Fixed32(_) => 5,
        };
        write_varint(u64::from(field.number) << 3 | wire_type, out);
        match field.value {
            Value::Varint(value) => write_varint(value, out),
            Value::Fixed64(value) => out.extend_from_slice(&value.to_le_bytes()),
            Value::Bytes(bytes) => {
                write_varint(bytes.len() as u64, out);
                out.extend_from_slice(bytes);
            }
            Value::Fixed32(value) => out.extend_from_slice(&value.to_le_bytes()),
        }
    }
}

/// A varint at the start of `bytes` and the bytes it took, at most 10; `None` when truncated or past 64 bits.
pub(crate) fn read_varint(bytes: &[u8]) -> Option<(u64, usize)> {
    let mut value: u64 = 0;
    for (index, &byte) in bytes.iter().take(10).enumerate() {
        if index == 9 && byte > 1 {
            return None;
        }
        value |= u64::from(byte & 0x7f) << (7 * index);
        if byte & 0x80 == 0 {
            return Some((value, index + 1));
        }
    }
    None
}

/// Appends `value` as a minimal varint.
pub(crate) fn write_varint(mut value: u64, out: &mut Vec<u8>) {
    while value >= 0x80 {
        // The mask keeps the low seven bits, so the cast never truncates anything kept.
        out.push((value & 0x7f) as u8 | 0x80);
        value >>= 7;
    }
    // Below 0x80 here, so it fits.
    out.push(u8::try_from(value).unwrap_or(0x7f));
}

#[cfg(test)]
mod tests {
    use super::*;

    fn read(bytes: &[u8]) -> Vec<Field<'_>> {
        fields(bytes)
            .collect::<Result<_, _>>()
            .unwrap_or_else(|error| panic!("{error}"))
    }

    fn refused(bytes: &[u8]) -> bool {
        matches!(fields(bytes).collect::<Result<Vec<_>, _>>(), Err(RelayError::Wire))
    }

    #[test]
    fn reads_every_wire_type() {
        let bytes = [
            0x08, 0x96, 0x01, // 1: varint 150
            0x11, 1, 0, 0, 0, 0, 0, 0, 0, // 2: fixed64 1
            0x1a, 0x02, b'h', b'i', // 3: bytes "hi"
            0x25, 2, 0, 0, 0, // 4: fixed32 2
        ];

        assert_eq!(
            read(&bytes),
            vec![
                Field {
                    number: 1,
                    value: Value::Varint(150)
                },
                Field {
                    number: 2,
                    value: Value::Fixed64(1)
                },
                Field {
                    number: 3,
                    value: Value::Bytes(b"hi")
                },
                Field {
                    number: 4,
                    value: Value::Fixed32(2)
                },
            ]
        );
    }

    #[test]
    fn encodes_back_to_the_same_bytes() {
        let bytes = [
            0x08, 0x96, 0x01, 0x11, 1, 0, 0, 0, 0, 0, 0, 0, 0x1a, 0x02, b'h', b'i', 0x25, 2, 0, 0, 0,
        ];
        let mut out = Vec::new();

        encode(&read(&bytes), &mut out);

        assert_eq!(out, bytes);
    }

    #[test]
    fn a_varint_not_minimal_does_not_encode_back() {
        let bytes = [0x08, 0x81, 0x00];
        let mut out = Vec::new();

        encode(&read(&bytes), &mut out);

        assert_ne!(out, bytes);
    }

    #[test]
    fn refuses_truncated_fields() {
        for bytes in [
            &[0x08][..],
            &[0x08, 0x80],
            &[0x11, 1, 2, 3],
            &[0x1a, 0x05, b'a'],
            &[0x1a],
            &[0x25, 1],
            &[0x80],
        ] {
            assert!(refused(bytes), "{bytes:?}");
        }
    }

    #[test]
    fn refuses_groups_and_field_zero() {
        for bytes in [&[0x0b][..], &[0x0c], &[0x0e], &[0x00, 0x01]] {
            assert!(refused(bytes), "{bytes:?}");
        }
    }

    #[test]
    fn stops_at_the_first_byte_that_is_not_a_field() {
        let mut read = fields(&[0x08, 0x01, 0x0b, 0x08, 0x02]);

        assert_eq!(
            read.next().map(Result::ok),
            Some(Some(Field {
                number: 1,
                value: Value::Varint(1)
            }))
        );
        assert!(matches!(read.next(), Some(Err(RelayError::Wire))));
        assert!(read.next().is_none());
    }

    #[test]
    fn debug_shows_only_the_length_of_bytes() {
        let shown = format!("{:?}", Value::Bytes(b"0123456789abcdef0123456789abcdef"));

        assert_eq!(shown, "Bytes(<32 bytes>)");
    }

    #[test]
    fn varints_round_trip_at_their_edges() {
        for value in [0, 1, 127, 128, 16_383, 16_384, u64::from(u32::MAX), u64::MAX] {
            let mut out = Vec::new();
            write_varint(value, &mut out);

            assert_eq!(read_varint(&out), Some((value, out.len())), "{value}");
        }
        assert_eq!(read_varint(&[0xff; 11]), None);
        let mut past = vec![0xff; 9];
        past.push(0x02);
        assert_eq!(read_varint(&past), None);
    }
}
