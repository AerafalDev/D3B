//! Each message of the live view decoded from bytes written by hand, field by field, as the client writes them (field
//! numbers as `src-tauri/protocol/game/` declares them, checked by hand against 3.7.13.17's schema).

use super::*;
use crate::protocol::game::characteristic::Kind;
use crate::relay::wire::{self, Field, Value};

fn message(fields: &[Field<'_>]) -> Vec<u8> {
    let mut out = Vec::new();
    wire::encode(fields, &mut out);
    out
}

fn varint(number: u32, value: i64) -> Field<'static> {
    Field {
        number,
        // A negative `int32` or `int64` is written as its 64-bit two's complement, as protobuf does.
        value: Value::Varint(value.cast_unsigned()),
    }
}

fn bytes(number: u32, value: &[u8]) -> Field<'_> {
    Field {
        number,
        value: Value::Bytes(value),
    }
}

fn packed(values: &[u64]) -> Vec<u8> {
    let mut out = Vec::new();
    for &value in values {
        wire::write_varint(value, &mut out);
    }
    out
}

fn decoded(readable: &str, bytes: &[u8]) -> Message {
    match Message::decode(readable, bytes) {
        Some(Ok(message)) => message,
        other => panic!("{readable}: {other:?}"),
    }
}

#[test]
fn reads_the_current_map_and_its_request() {
    let body = message(&[varint(1, 153_092_354)]);

    assert_eq!(
        decoded("MapCurrentEvent", &body),
        Message::MapCurrentEvent(game::MapCurrentEvent { map_id: 153_092_354 })
    );
    assert_eq!(
        decoded("MapInformationRequest", &body),
        Message::MapInformationRequest(game::MapInformationRequest { map_id: 153_092_354 })
    );
}

#[test]
fn reads_what_is_on_the_map_and_drops_what_it_does_not_declare() {
    let stated = message(&[varint(2, 1), varint(3, 465_210), varint(4, 287), varint(5, 2)]);
    let disposition = message(&[varint(3, 300), varint(4, 3)]);
    let actor = message(&[varint(1, -20), bytes(2, &disposition), bytes(3, b"\x0a\x00")]);
    let skill = message(&[varint(1, 7), varint(3, 45)]);
    let disabled = message(&[varint(1, 8), varint(3, 46)]);
    let interactive = message(&[
        varint(2, 1),
        bytes(3, &disabled),
        varint(4, 82),
        bytes(5, &skill),
        varint(6, 465_210),
    ]);
    let body = message(&[
        bytes(2, &stated),
        bytes(3, &actor),
        bytes(7, &interactive),
        varint(10, 153_092_354),
        varint(12, 450),
        varint(13, 1),
    ]);

    let Message::MapInformationEvent(map) = decoded("MapInformationEvent", &body) else {
        panic!("not the map's information");
    };

    assert_eq!((map.map_id, map.sub_area_id), (153_092_354, 450));
    assert_eq!(
        map.stated_elements,
        [game::StatedElement {
            on_current_map: true,
            element_id: 465_210,
            cell_id: 287,
            state: 2
        }]
    );
    assert_eq!(map.actors.len(), 1);
    assert_eq!(map.actors[0].id, -20);
    assert_eq!(
        map.actors[0].disposition,
        Some(game::ActorDisposition {
            cell_id: Some(300),
            direction: 3
        })
    );
    let element = &map.interactive_elements[0];
    assert_eq!(
        (element.on_current_map, element.interactive_id, element.element_id),
        (true, 82, 465_210)
    );
    assert_eq!(
        element.disabled_skills,
        [game::InteractiveSkill {
            instance_id: 8,
            skill_id: 46
        }]
    );
    assert_eq!(
        map.interactive_elements[0].enabled_skills,
        [game::InteractiveSkill {
            instance_id: 7,
            skill_id: 45
        }]
    );
    // Field 13, which our `.proto` lacks, is not encoded back: the frame is "read", not "understood".
    assert!(decoded("MapInformationEvent", &body).encode().len() < body.len());
}

#[test]
fn reads_the_character_chosen() {
    let look = message(&[varint(3, 9)]);
    let basics = message(&[varint(3, 200), bytes(5, &look), bytes(6, b"Lyra")]);
    let character = message(&[varint(3, 1_234_567), bytes(4, &basics)]);
    let success = message(&[bytes(1, &character)]);
    let body = message(&[bytes(1, &success)]);

    let Message::CharacterSelectionEvent(selection) = decoded("CharacterSelectionEvent", &body) else {
        panic!("not the selection");
    };

    let Some(game::character_selection_event::Result::Success(success)) = selection.result else {
        panic!("not a success: {selection:?}");
    };
    let character = success.character.unwrap_or_default();
    let basics = character.basics.unwrap_or_default();
    assert_eq!(character.id, 1_234_567);
    assert_eq!((basics.level, basics.name.as_str()), (Some(200), "Lyra"));
    assert_eq!(basics.look.map(|look| look.breed_id), Some(9));
}

#[test]
fn reads_a_characteristic_of_each_kind() {
    let value = message(&[varint(2, 55)]);
    let usable = message(&[varint(7, 3)]);
    let characteristics = [
        message(&[varint(2, 0), bytes(3, &value)]),
        message(&[bytes(1, &usable), varint(2, 1)]),
        message(&[varint(2, 2), bytes(5, &[])]),
    ];
    let stats: Vec<Field<'_>> = characteristics.iter().map(|one| bytes(1, one)).collect();
    let stats = message(&stats);
    let body = message(&[bytes(1, &stats)]);

    let Message::CharacterStatsEvent(event) = decoded("CharacterStatsEvent", &body) else {
        panic!("not the characteristics");
    };

    let kinds: Vec<_> = event
        .stats
        .unwrap_or_default()
        .characteristics
        .into_iter()
        .map(|characteristic| (characteristic.id, characteristic.kind))
        .collect();
    assert_eq!(
        kinds,
        [
            (0, Some(Kind::Value(game::CharacteristicValue { total: 55 }))),
            (1, Some(Kind::Usable(game::UsableCharacteristic { spent: 3 }))),
            (2, Some(Kind::Detailed(game::DetailedCharacteristic {}))),
        ]
    );
}

#[test]
fn prost_writes_a_oneof_where_its_first_field_would_be() {
    let value = message(&[varint(2, 55)]);
    // As the client writes it, in number order: the id (2), then the oneof's member, the value (3).
    let characteristic = message(&[varint(2, 6), bytes(3, &value)]);
    let body = message(&[bytes(1, &message(&[bytes(1, &characteristic)]))]);

    let encoded = decoded("CharacterStatsEvent", &body).encode();

    // prost writes the oneof `kind` (fields 1, 3 and 5) at field 1's place, before the id: the four outcomes compare
    // fields in number order, not the bytes.
    let reordered = message(&[bytes(3, &value), varint(2, 6)]);
    assert_eq!(encoded, message(&[bytes(1, &message(&[bytes(1, &reordered)]))]));
}

#[test]
fn the_declared_fields_encode_back_to_their_own_bytes() {
    let cells = packed(&[287, 301, 315]);
    let stated = message(&[varint(3, 465_210), varint(5, 3)]);
    let interactive = message(&[varint(4, 82), varint(6, 465_210)]);
    let actor = message(&[varint(1, 1_234_567)]);
    // Each body holds only fields our `.proto` declares: one read with another number or kind would not come back.
    for (readable, body) in [
        ("ContextCreationEvent", message(&[varint(1, 1)])),
        ("ContextDestroyEvent", Vec::new()),
        ("MapActorShownEvent", message(&[bytes(1, &actor)])),
        ("MapActorRemovedEvent", message(&[varint(1, -20)])),
        ("MapMovementEvent", message(&[bytes(1, &cells), varint(4, 1_234_567)])),
        ("StatedElementUpdatedEvent", message(&[bytes(1, &stated)])),
        ("InteractiveElementUpdatedEvent", message(&[bytes(1, &interactive)])),
        ("LifeRegenStartEvent", message(&[varint(1, 20)])),
        (
            "LifeRegenEndEvent",
            message(&[varint(1, 5_000), varint(3, 4_200), varint(4, 0)]),
        ),
        ("InventoryWeightEvent", message(&[varint(1, 1_200), varint(2, 3_000)])),
    ] {
        assert_eq!(decoded(readable, &body).encode(), body, "{readable}");
    }
}

#[test]
fn a_field_of_another_number_does_not_come_back() {
    // The weight's fields are 1 and 2: a third is dropped.
    let body = message(&[varint(1, 1_200), varint(3, 3_000)]);

    assert_eq!(
        decoded("InventoryWeightEvent", &body),
        Message::InventoryWeightEvent(game::InventoryWeightEvent {
            weight: 1_200,
            max_weight: 0
        })
    );
    assert_ne!(decoded("InventoryWeightEvent", &body).encode(), body);
}

#[test]
fn reads_a_failed_selection() {
    let body = message(&[bytes(2, &[])]);

    assert_eq!(
        decoded("CharacterSelectionEvent", &body),
        Message::CharacterSelectionEvent(game::CharacterSelectionEvent {
            result: Some(game::character_selection_event::Result::Failure(
                game::character_selection_event::Failure {}
            )),
        })
    );
}

#[test]
fn a_name_no_message_bears_decodes_nothing() {
    assert!(Message::decode("MapCurrentEvents", &[]).is_none());
    assert!(Message::decode("jow", &[]).is_none());
}

#[test]
fn bytes_that_are_not_the_message_do_not_decode() {
    let truncated = message(&[varint(1, 153_092_354)]);
    for (readable, body) in [
        ("MapCurrentEvent", &truncated[..truncated.len() - 1]),
        // Field 1 written as bytes where the message declares a varint.
        ("MapCurrentEvent", &message(&[bytes(1, b"x")])[..]),
        ("MapInformationEvent", &message(&[bytes(3, &[0xff])])[..]),
    ] {
        assert!(
            matches!(Message::decode(readable, body), Some(Err(_))),
            "{readable}: {body:?}"
        );
    }
}
