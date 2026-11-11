//! The connection phase through the relay (`docs/relay.md`, "The connections"; inferred, `main:docs/client.md`, "The
//! connection phase on the wire", 3.7.12.12 and 3.7.13.17): the client's identification and server selection, and
//! their answers. Every byte passes untouched but the server-selection answer, whose game host and ports become the
//! relay's, so that the game connection comes through it too.

use std::sync::Mutex;
use std::time::Instant;

use super::ankama_config::is_host_name;
use super::handover::{GameServer, Handover, Ticket};
use super::wire::{self, Field, Value};
use super::{ADDRESS, GAME_PORT, frame};

/// Whether a client frame's body asks for a server: `request = 1 { id = 1, server_selection = 4 }` (path 1.4,
/// `artifacts/studies/relay.md`, addendum).
fn is_selection_request(body: &[u8]) -> bool {
    only(body, 1).is_some_and(|request| {
        wire::fields(request)
            .map_while(Result::ok)
            .any(|field| field.number == 4)
    })
}

/// The selection's answer rewritten to send the game connection to the relay, as a whole frame; its ticket and real
/// game server are put in the handover. `None` when `body` is not that answer, read whole and of the expected shape:
/// `response = 2 { id = 1, server_selection = 4 { success = 1 { ticket = 1, host = 2, ports = 3 } } }`, a ticket of
/// 32 letters and digits, a host name, packed ports from 1 to 65535 (path 2.4.1, `artifacts/studies/relay.md`,
/// addendum; the shape checks are the owner's choice for builds not measured, 2026-11-11).
fn rewrite_selection(body: &[u8], handover: &Mutex<Handover>, now: Instant) -> Option<Vec<u8>> {
    let response = whole(only(body, 2)?)?;
    let selection = bytes_of(&response, 4)?;
    let success = whole(only(selection, 1)?)?;
    let [
        Field {
            number: 1,
            value: Value::Bytes(ticket),
        },
        Field {
            number: 2,
            value: Value::Bytes(host),
        },
        Field {
            number: 3,
            value: Value::Bytes(ports),
        },
    ] = success.as_slice()
    else {
        return None;
    };
    let host = std::str::from_utf8(host).ok().filter(|host| is_host_name(host))?;
    let waiting = Ticket::new(ticket)?;
    let port = *packed_ports(ports)?.first()?;

    let relay_host = ADDRESS.to_string();
    let mut relay_ports = Vec::new();
    wire::write_varint(u64::from(GAME_PORT), &mut relay_ports);
    let new_success = encoded(&[
        Field {
            number: 1,
            value: Value::Bytes(ticket),
        },
        Field {
            number: 2,
            value: Value::Bytes(relay_host.as_bytes()),
        },
        Field {
            number: 3,
            value: Value::Bytes(&relay_ports),
        },
    ]);
    let new_selection = encoded(&[Field {
        number: 1,
        value: Value::Bytes(&new_success),
    }]);
    let new_response = encoded(&replaced(&response, 4, &new_selection));
    let new_body = encoded(&[Field {
        number: 2,
        value: Value::Bytes(&new_response),
    }]);

    let server = GameServer {
        host: host.to_owned(),
        port,
    };
    handover
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        .put(waiting, server, now);
    let mut frame = Vec::with_capacity(new_body.len() + 2);
    frame::encode(&new_body, &mut frame);
    Some(frame)
}

/// The fields of `message` when it encodes back to its own bytes, so that nothing was missed.
fn whole(message: &[u8]) -> Option<Vec<Field<'_>>> {
    let fields: Vec<Field<'_>> = wire::fields(message).collect::<Result<_, _>>().ok()?;
    (encoded(&fields) == message).then_some(fields)
}

/// The value of `message`'s only field, when it is the length-delimited field `number` and the message is whole.
fn only(message: &[u8], number: u32) -> Option<&[u8]> {
    match whole(message)?.as_slice() {
        [
            Field {
                number: found,
                value: Value::Bytes(bytes),
            },
        ] if *found == number => Some(bytes),
        _ => None,
    }
}

/// The value of the one length-delimited field `number` among `fields`.
fn bytes_of<'a>(fields: &[Field<'a>], number: u32) -> Option<&'a [u8]> {
    let mut found = fields.iter().filter(|field| field.number == number);
    match (found.next(), found.next()) {
        (
            Some(Field {
                value: Value::Bytes(bytes),
                ..
            }),
            None,
        ) => Some(bytes),
        _ => None,
    }
}

fn replaced<'a>(fields: &[Field<'a>], number: u32, bytes: &'a [u8]) -> Vec<Field<'a>> {
    fields
        .iter()
        .map(|field| {
            if field.number == number {
                Field {
                    number,
                    value: Value::Bytes(bytes),
                }
            } else {
                *field
            }
        })
        .collect()
}

fn encoded(fields: &[Field<'_>]) -> Vec<u8> {
    let mut out = Vec::new();
    wire::encode(fields, &mut out);
    out
}

/// Packed varints as ports, each from 1 to 65535, at least one.
fn packed_ports(mut bytes: &[u8]) -> Option<Vec<u16>> {
    let mut ports = Vec::new();
    while !bytes.is_empty() {
        let (value, used) = wire::read_varint(bytes)?;
        ports.push(u16::try_from(value).ok().filter(|&port| port != 0)?);
        bytes = &bytes[used..];
    }
    (!ports.is_empty()).then_some(ports)
}
#[cfg(test)]
mod tests {
    use super::*;

    const TICKET: &[u8] = b"0123456789abcdef0123456789ABCDEF";

    fn message(fields: &[Field<'_>]) -> Vec<u8> {
        encoded(fields)
    }

    fn bytes(number: u32, value: &[u8]) -> Field<'_> {
        Field {
            number,
            value: Value::Bytes(value),
        }
    }

    fn packed(ports: &[u64]) -> Vec<u8> {
        let mut out = Vec::new();
        for &port in ports {
            wire::write_varint(port, &mut out);
        }
        out
    }

    /// The server's answer to the selection, shaped as on 3.7.13.17, with `success` holding `fields`.
    fn answer_with(fields: &[Field<'_>]) -> Vec<u8> {
        let success = message(fields);
        let selection = message(&[bytes(1, &success)]);
        let response = message(&[bytes(1, b"7"), bytes(4, &selection)]);
        message(&[bytes(2, &response)])
    }

    fn answer() -> Vec<u8> {
        let ports = packed(&[5555, 443]);
        answer_with(&[bytes(1, TICKET), bytes(2, b"game.example.com"), bytes(3, &ports)])
    }

    fn selection_request() -> Vec<u8> {
        let selection = message(&[Field {
            number: 1,
            value: Value::Varint(42),
        }]);
        let request = message(&[bytes(1, b"3"), bytes(4, &selection)]);
        message(&[bytes(1, &request)])
    }

    fn identification() -> Vec<u8> {
        let identification = message(&[bytes(1, b"device"), bytes(3, b"3.7.13.17")]);
        let request = message(&[bytes(1, b"1"), bytes(3, &identification)]);
        message(&[bytes(1, &request)])
    }

    /// The parts of a rewritten answer: its ticket, host and ports.
    fn read_answer(frame: &[u8]) -> (Vec<u8>, String, Vec<u16>) {
        let (header, body) = frame::header(frame)
            .unwrap_or_else(|error| panic!("{error}"))
            .unwrap_or_else(|| panic!("not a whole frame"));
        assert_eq!(frame.len(), header + body);
        let response = whole(only(&frame[header..], 2).unwrap_or_default()).unwrap_or_default();
        assert_eq!(response[0], bytes(1, b"7"));
        let selection = bytes_of(&response, 4).unwrap_or_default();
        let success = whole(only(selection, 1).unwrap_or_default()).unwrap_or_default();
        let [
            Field {
                value: Value::Bytes(ticket),
                ..
            },
            Field {
                value: Value::Bytes(host),
                ..
            },
            Field {
                value: Value::Bytes(ports),
                ..
            },
        ] = success.as_slice()
        else {
            panic!("not an answer: {success:?}");
        };
        (
            ticket.to_vec(),
            String::from_utf8_lossy(host).into_owned(),
            packed_ports(ports).unwrap_or_default(),
        )
    }

    #[test]
    fn rewrites_the_answer_to_the_relays_game_port() {
        let handover = Mutex::new(Handover::default());

        let rewritten =
            rewrite_selection(&answer(), &handover, Instant::now()).unwrap_or_else(|| panic!("not rewritten"));

        assert_eq!(
            read_answer(&rewritten),
            (TICKET.to_vec(), "127.0.13.59".to_owned(), vec![5556])
        );
        let first_frame = message(&[bytes(1, TICKET)]);
        let waiting = handover
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .take_from(&first_frame, Instant::now());
        assert_eq!(
            waiting,
            Some(GameServer {
                host: "game.example.com".into(),
                port: 5555
            })
        );
    }

    #[test]
    fn leaves_an_answer_of_another_shape() {
        let ports = packed(&[5555]);
        let short = b"0123456789abcdef";
        let not_minimal = [0x0a, 0x81, 0x00];
        for body in [
            answer_with(&[bytes(1, short), bytes(2, b"game.example.com"), bytes(3, &ports)]),
            answer_with(&[bytes(1, TICKET), bytes(2, b"game_server"), bytes(3, &ports)]),
            answer_with(&[bytes(1, TICKET), bytes(2, b"game.example.com"), bytes(3, &packed(&[0]))]),
            answer_with(&[
                bytes(1, TICKET),
                bytes(2, b"game.example.com"),
                bytes(3, &packed(&[65_536])),
            ]),
            answer_with(&[bytes(1, TICKET), bytes(2, b"game.example.com"), bytes(3, &[])]),
            answer_with(&[
                bytes(1, TICKET),
                bytes(2, b"game.example.com"),
                bytes(3, &ports),
                bytes(4, b"x"),
            ]),
            answer_with(&[bytes(2, b"game.example.com"), bytes(1, TICKET), bytes(3, &ports)]),
            message(&[bytes(
                2,
                &message(&[bytes(1, b"7"), bytes(4, &message(&[bytes(2, b"\x01")]))]),
            )]),
            not_minimal.to_vec(),
            identification(),
            Vec::new(),
        ] {
            let handover = Mutex::new(Handover::default());

            assert_eq!(rewrite_selection(&body, &handover, Instant::now()), None, "{body:?}");
            assert_eq!(
                handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).len(),
                0
            );
        }
    }

    #[test]
    fn knows_the_selection_request() {
        assert!(is_selection_request(&selection_request()));
        assert!(!is_selection_request(&identification()));
        assert!(!is_selection_request(&answer()));
        assert!(!is_selection_request(&[0xff]));
    }
}
