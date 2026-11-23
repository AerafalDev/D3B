use super::*;
use crate::protocol::envelope;
use crate::relay::Side;

fn pins(messages: &str) -> Pins {
    let json = format!(
        r#"{{"build": "3.7.13.17", "schemaSha256": "{}", "messages": {{{messages}}}, "secrets": []}}"#,
        "d74dfab609f87963fdd6230f5936c547da06c6deff9873cdfd75915d0e9b4980"
    );
    Pins::parse("3.7.13.17", &json).unwrap_or_else(|error| panic!("{error}"))
}

fn movement() -> Pins {
    pins(r#""MapMovementRequest": "jok", "MapChangeRequest": "joc""#)
}

/// A walk of two cells east then one south-east, on map 191106048.
fn walk() -> Request {
    Request::MapMovementRequest(game::MapMovementRequest {
        key_movements: vec![300, 302, 1 << 12 | 316],
        map_id: 191_106_048,
        flag_3: false,
    })
}

#[test]
fn encodes_a_walk_as_the_client_frames_it() {
    let frame = walk().frame(&movement()).unwrap_or_else(|error| panic!("{error}"));

    let mut value = Vec::new();
    walk_value(&mut value);
    let mut any = vec![0x0a, 19];
    any.extend_from_slice(b"type.ankama.com/jok");
    any.push(0x12);
    any.push(u8::try_from(value.len()).unwrap_or_default());
    any.extend_from_slice(&value);
    // The id -1, a varint of ten bytes, then the `Any`.
    let mut request = vec![0x08, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01, 0x12];
    request.push(u8::try_from(any.len()).unwrap_or_default());
    request.extend_from_slice(&any);
    let mut root = vec![0x22, u8::try_from(request.len()).unwrap_or_default()];
    root.extend_from_slice(&request);
    let mut expected = vec![u8::try_from(root.len()).unwrap_or_default()];
    expected.extend_from_slice(&root);
    assert_eq!(frame, expected);
}

/// The walk's message as prost writes it: field 1 packed, then the map.
fn walk_value(out: &mut Vec<u8>) {
    let mut packed = Vec::new();
    for cell in [300_u64, 302, 1 << 12 | 316] {
        wire::write_varint(cell, &mut packed);
    }
    out.push(0x0a);
    out.push(u8::try_from(packed.len()).unwrap_or_default());
    out.extend_from_slice(&packed);
    out.push(0x10);
    wire::write_varint(191_106_048, out);
}

#[test]
fn a_frame_opens_as_the_client_s_and_decodes_to_its_request() {
    let frame = walk().frame(&movement()).unwrap_or_else(|error| panic!("{error}"));
    let Ok(Some((header, _))) = frame::header(&frame) else {
        panic!("not a frame");
    };

    let content = envelope::open(Side::Client, &frame[header..]);

    let content = content.unwrap_or_else(|| panic!("not the client's envelope"));
    assert_eq!(content.name, "jok");
    let decoded = Request::decode("MapMovementRequest", content.value).and_then(Result::ok);
    assert_eq!(decoded, Some(walk()));
}

#[test]
fn an_empty_request_is_sent_without_a_value() {
    let empty = Request::MapChangeRequest(game::MapChangeRequest::default());

    let frame = empty.frame(&movement()).unwrap_or_else(|error| panic!("{error}"));

    let content = envelope::open(Side::Client, &frame[1..]).unwrap_or_else(|| panic!("not the client's envelope"));
    assert_eq!((content.name, content.value), ("joc", &[][..]));
    // Its `Any` holds the URL alone: 1 + 1 + 19 bytes, the request 11 + 2 more, the root 2 more, the header 1.
    assert_eq!(frame.len(), 37, "an empty value written");
}

#[test]
fn a_request_the_build_does_not_pin_is_refused() {
    let walks_only = pins(r#""MapMovementRequest": "jok""#);
    let change = Request::MapChangeRequest(game::MapChangeRequest {
        flag_1: false,
        map_id: 191_106_050,
    });

    assert_eq!(
        change.frame(&walks_only),
        Err(RequestError::NotPinned {
            readable: "MapChangeRequest"
        })
    );
    assert!(walk().frame(&walks_only).is_ok());
}

#[test]
fn every_request_is_a_message_the_pins_may_pin() {
    for readable in ENCODED {
        assert!(
            crate::protocol::message::READABLE.contains(readable),
            "{readable} is not decoded"
        );
    }
    assert_eq!(ENCODED, ["MapMovementRequest", "MapChangeRequest"]);
}
