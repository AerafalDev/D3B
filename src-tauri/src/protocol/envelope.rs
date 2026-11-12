//! The envelope of a game frame (`docs/protocol.md`, "On the wire"; verified, the 3,048,392 frames of the owner's 54
//! game connections of 3.7.13.17): a client frame is the root's field 4, a request `{ int32 id = 1, Any = 2 }`; a
//! server frame the root's field 3, an event `{ Any = 1 }`, or field 1, a response `{ int32 id = 1, Any = 2 }`; an
//! `Any` is `{ type_url = 1, value = 2 }`, its URL `type.ankama.com/` then the message's name. It is read by the wire
//! format alone, with no allocation: no build's names are needed to open it.

use crate::relay::Side;
use crate::relay::wire::{self, Field, Value};

/// A game frame's message, its envelope opened.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Content<'a> {
    /// The message's name in the build, such as `jow`.
    pub(crate) name: &'a str,
    /// The message's bytes.
    pub(crate) value: &'a [u8],
}

/// The pattern of a length-delimited field.
macro_rules! bytes {
    ($number:pat, $value:ident) => {
        Field {
            number: $number,
            value: Value::Bytes($value),
        }
    };
}

/// The prefix of every type URL (verified, 3.7.13.17).
const TYPE_URL: &str = "type.ankama.com/";

/// The longest name a type URL may hold: 3.7.13.17's are three letters (verified); a longer one is no longer the
/// envelope measured.
const NAME_LIMIT: usize = 8;

/// The message in `body`, a frame `side` sent; `None` when its envelope is not as measured.
pub(crate) fn open(side: Side, body: &[u8]) -> Option<Content<'_>> {
    let any = match (side, only(body)?) {
        (Side::Client, (4, request)) => with_id(request)?,
        (Side::Server, (1, response)) => with_id(response)?,
        (Side::Server, (3, event)) => match only(event)? {
            (1, any) => any,
            _ => return None,
        },
        _ => return None,
    };
    let (url, value) = match fields::<2>(any)? {
        [Some(bytes!(1, url)), None] => (url, &[][..]),
        [Some(bytes!(1, url)), Some(bytes!(2, value))] => (url, value),
        _ => return None,
    };
    let name = std::str::from_utf8(url).ok()?.strip_prefix(TYPE_URL)?;
    let named = (1..=NAME_LIMIT).contains(&name.len()) && name.bytes().all(|byte| byte.is_ascii_lowercase());
    named.then_some(Content { name, value })
}

/// The `Any` of a request or a response: its id, which the client sets to -1 or from 2 (verified, 3.7.13.17), then
/// the `Any`; an id of 0 is not written.
fn with_id(message: &[u8]) -> Option<&[u8]> {
    match fields::<2>(message)? {
        [Some(bytes!(2, any)), None]
        | [
            Some(Field {
                number: 1,
                value: Value::Varint(_),
            }),
            Some(bytes!(2, any)),
        ] => Some(any),
        _ => None,
    }
}

/// The number and value of the one length-delimited field of `message`.
fn only(message: &[u8]) -> Option<(u32, &[u8])> {
    match fields::<1>(message)? {
        [Some(bytes!(number, value))] => Some((number, value)),
        _ => None,
    }
}

/// At most `N` fields of `message`, which holds no more; `None` when it does, or is not fields.
fn fields<const N: usize>(message: &[u8]) -> Option<[Option<Field<'_>>; N]> {
    let mut read = wire::fields(message);
    let mut found = [None; N];
    for slot in &mut found {
        match read.next() {
            Some(Ok(field)) => *slot = Some(field),
            Some(Err(_)) => return None,
            None => break,
        }
    }
    read.next().is_none().then_some(found)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn message(fields: &[Field<'_>]) -> Vec<u8> {
        let mut out = Vec::new();
        wire::encode(fields, &mut out);
        out
    }

    fn bytes(number: u32, value: &[u8]) -> Field<'_> {
        Field {
            number,
            value: Value::Bytes(value),
        }
    }

    fn id(value: i64) -> Field<'static> {
        Field {
            number: 1,
            value: Value::Varint(value.cast_unsigned()),
        }
    }

    fn any(url: &[u8], value: &[u8]) -> Vec<u8> {
        message(&[bytes(1, url), bytes(2, value)])
    }

    const VALUE: &[u8] = b"\x08\x82\xc0\xb2\x48";

    #[test]
    fn opens_a_request_an_event_and_a_response() {
        let any = any(b"type.ankama.com/kjt", VALUE);
        let request = message(&[bytes(4, &message(&[id(-1), bytes(2, &any)]))]);
        let event = message(&[bytes(3, &message(&[bytes(1, &any)]))]);
        let response = message(&[bytes(1, &message(&[id(2), bytes(2, &any)]))]);
        let expected = Some(Content {
            name: "kjt",
            value: VALUE,
        });

        assert_eq!(open(Side::Client, &request), expected);
        assert_eq!(open(Side::Server, &event), expected);
        assert_eq!(open(Side::Server, &response), expected);
    }

    #[test]
    fn opens_an_empty_message_and_a_request_of_id_0() {
        // An empty message's `Any` holds no value, and an id of 0 is not written.
        let empty = message(&[bytes(1, b"type.ankama.com/kjy")]);
        let event = message(&[bytes(3, &message(&[bytes(1, &empty)]))]);
        let request = message(&[bytes(4, &message(&[bytes(2, &empty)]))]);
        let expected = Some(Content {
            name: "kjy",
            value: &[],
        });

        assert_eq!(open(Side::Server, &event), expected);
        assert_eq!(open(Side::Client, &request), expected);
    }

    #[test]
    fn a_frame_of_the_other_side_or_another_root_is_not_opened() {
        let any = any(b"type.ankama.com/kjt", VALUE);
        let request = message(&[bytes(4, &message(&[id(-1), bytes(2, &any)]))]);
        let event = message(&[bytes(3, &message(&[bytes(1, &any)]))]);
        let both = [request.clone(), event.clone()].concat();
        for (side, body) in [
            (Side::Server, request),
            (Side::Client, event),
            (Side::Server, both),
            (Side::Server, message(&[bytes(2, &message(&[bytes(1, &any)]))])),
            (Side::Client, message(&[bytes(1, &message(&[id(2), bytes(2, &any)]))])),
            (Side::Server, message(&[bytes(3, &message(&[bytes(2, &any)]))])),
            (Side::Client, Vec::new()),
            (Side::Client, vec![0xff]),
        ] {
            assert_eq!(open(side, &body), None, "{side:?} {body:?}");
        }
    }

    #[test]
    fn an_envelope_not_as_measured_is_not_opened() {
        let any = any(b"type.ankama.com/kjt", VALUE);
        for inner in [
            message(&[bytes(2, &any), id(-1)]),
            message(&[id(-1), bytes(2, &any), bytes(3, b"")]),
            message(&[id(-1)]),
            message(&[id(-1), bytes(2, &[0xff])]),
            message(&[id(-1), bytes(2, &message(&[bytes(2, VALUE)]))]),
            message(&[bytes(1, b"7"), bytes(2, &any)]),
            message(&[
                id(-1),
                bytes(2, &message(&[bytes(1, b"type.ankama.com/kjt"), bytes(3, VALUE)])),
            ]),
            message(&[
                id(-1),
                bytes(
                    2,
                    &message(&[bytes(1, b"type.ankama.com/kjt"), bytes(2, VALUE), bytes(3, b"")]),
                ),
            ]),
        ] {
            let request = message(&[bytes(4, &inner)]);

            assert_eq!(open(Side::Client, &request), None, "{inner:?}");
        }
    }

    #[test]
    fn opens_a_name_of_one_to_eight_letters() {
        for name in ["k", "abcdefgh"] {
            let url = format!("type.ankama.com/{name}");
            let event = message(&[bytes(3, &message(&[bytes(1, &any(url.as_bytes(), VALUE))]))]);

            assert_eq!(open(Side::Server, &event).map(|content| content.name), Some(name));
        }
    }

    #[test]
    fn a_type_url_of_another_shape_is_not_opened() {
        for url in [
            &b"type.googleapis.com/kjt"[..],
            b"type.ankama.com/",
            b"type.ankama.com/KJT",
            b"type.ankama.com/abcdefghi",
            b"type.ankama.com/a.b",
            b"\xff",
        ] {
            let event = message(&[bytes(3, &message(&[bytes(1, &any(url, VALUE))]))]);

            assert_eq!(open(Side::Server, &event), None, "{url:?}");
        }
    }
}
