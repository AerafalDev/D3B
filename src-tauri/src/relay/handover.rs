use std::borrow::Borrow;
use std::collections::HashMap;
use std::fmt;
use std::time::{Duration, Instant};

use super::wire::{self, Value};

/// The game servers the connection phase sent clients to, each waiting for its game connection under the ticket the
/// client will present (`docs/relay.md`, "The connections"; inferred, `main:docs/client.md`, D34, 3.7.12.12 and
/// 3.7.13.17). A ticket is taken once, within [`Handover::LIFETIME`].
#[derive(Debug, Default)]
pub(crate) struct Handover {
    waiting: HashMap<Ticket, Waiting>,
}

/// The ticket of a game connection: it identifies the account, so its `Debug` never shows it.
#[derive(Clone, PartialEq, Eq, Hash)]
pub(crate) struct Ticket(Vec<u8>);

/// Where the game connection of a ticket goes: Ankama's game server, on the first port it listed, the one the client
/// connects to (inferred, `main:docs/client-game.md`, "The game connection", 3.7.12.12).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct GameServer {
    pub(crate) host: String,
    pub(crate) port: u16,
}

#[derive(Debug)]
struct Waiting {
    server: GameServer,
    until: Instant,
}

impl Handover {
    /// How long a ticket waits for its game connection; the client connects within a second of the answer.
    pub(crate) const LIFETIME: Duration = Duration::from_secs(60);

    /// How many levels of nested messages [`Handover::take_from`] reads below the frame's own; the ticket is four
    /// levels down (field path 4.2.2.1, inferred, `main:docs/client-game.md`, 3.7.13.17). It bounds the work a hostile
    /// frame can cost.
    const DEPTH: usize = 8;

    /// Keeps `server` for the game connection presenting `ticket`, from `now`.
    pub(crate) fn put(&mut self, ticket: Ticket, server: GameServer, now: Instant) {
        self.waiting.retain(|_, waiting| waiting.until > now);
        self.waiting.insert(
            ticket,
            Waiting {
                server,
                until: now + Self::LIFETIME,
            },
        );
    }

    /// The game server waiting for a ticket found in `frame`, the client's first frame of a game connection, removed
    /// so it is taken once. The ticket is found among the frame's length-delimited values by the wire format alone,
    /// so that no game mapping is needed (inferred, `main:docs/client-game.md`, BR1).
    pub(crate) fn take_from(&mut self, frame: &[u8], now: Instant) -> Option<GameServer> {
        self.waiting.retain(|_, waiting| waiting.until > now);
        if self.waiting.is_empty() {
            return None;
        }
        let ticket = self.find(frame, Self::DEPTH)?;
        self.waiting.remove(&ticket).map(|waiting| waiting.server)
    }

    /// How many game connections are waiting.
    #[cfg_attr(not(test), expect(dead_code, reason = "only the tests count what waits"))]
    pub(crate) fn len(&self) -> usize {
        self.waiting.len()
    }

    fn find(&self, message: &[u8], depth: usize) -> Option<Ticket> {
        wire::fields(message)
            .map_while(Result::ok)
            .find_map(|field| match field.value {
                Value::Bytes(bytes) if bytes.len() == Ticket::LENGTH && self.waiting.contains_key(bytes) => {
                    Ticket::new(bytes)
                }
                Value::Bytes(bytes) if depth > 0 => self.find(bytes, depth - 1),
                _ => None,
            })
    }
}

impl Ticket {
    /// A ticket's length: 32 hexadecimal characters (inferred, `main:docs/client-game.md`, BR1, 3.7.13.17).
    pub(crate) const LENGTH: usize = 32;

    /// The ticket of these bytes, as the server-selection answer holds it; `None` unless [`Ticket::LENGTH`] letters and
    /// digits, so that no short value of a frame can match one.
    pub(crate) fn new(bytes: &[u8]) -> Option<Self> {
        (bytes.len() == Self::LENGTH && bytes.iter().all(u8::is_ascii_alphanumeric)).then(|| Self(bytes.to_vec()))
    }
}

// A ticket hashes as its bytes do, so that a frame's values are looked up without a copy each.
impl Borrow<[u8]> for Ticket {
    fn borrow(&self) -> &[u8] {
        &self.0
    }
}

impl fmt::Debug for Ticket {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str("Ticket(<redacted>)")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const TICKET: &[u8] = b"0123456789abcdef0123456789abcdef";
    const OTHER: &[u8] = b"fedcba9876543210fedcba9876543210";

    fn ticket(bytes: &[u8]) -> Ticket {
        Ticket::new(bytes).unwrap_or_else(|| panic!("not a ticket"))
    }

    /// `ticket` nested `levels` messages below the frame's own level.
    fn nested(ticket: &[u8], levels: usize) -> Vec<u8> {
        let mut message = Vec::new();
        wire::encode(
            &[wire::Field {
                number: 1,
                value: Value::Bytes(ticket),
            }],
            &mut message,
        );
        for _ in 0..levels {
            let mut outer = Vec::new();
            wire::encode(
                &[wire::Field {
                    number: 2,
                    value: Value::Bytes(&message),
                }],
                &mut outer,
            );
            message = outer;
        }
        message
    }

    fn server() -> GameServer {
        GameServer {
            host: "game.example.com".into(),
            port: 5555,
        }
    }

    /// A first game frame shaped as the client's: `request = 4 { id = 1, Any = 2 { url = 1, value = 2 {
    /// ticket = 1, language = 2 } } }`.
    fn first_frame(ticket: &[u8]) -> Vec<u8> {
        let mut authentication = Vec::new();
        wire::encode(
            &[
                wire::Field {
                    number: 1,
                    value: Value::Bytes(ticket),
                },
                wire::Field {
                    number: 2,
                    value: Value::Bytes(b"fr"),
                },
            ],
            &mut authentication,
        );
        let mut any = Vec::new();
        wire::encode(
            &[
                wire::Field {
                    number: 1,
                    value: Value::Bytes(b"type.ankama.com/abc"),
                },
                wire::Field {
                    number: 2,
                    value: Value::Bytes(&authentication),
                },
            ],
            &mut any,
        );
        let mut request = Vec::new();
        wire::encode(
            &[
                wire::Field {
                    number: 1,
                    value: Value::Varint(u64::MAX),
                },
                wire::Field {
                    number: 2,
                    value: Value::Bytes(&any),
                },
            ],
            &mut request,
        );
        let mut frame = Vec::new();
        wire::encode(
            &[wire::Field {
                number: 4,
                value: Value::Bytes(&request),
            }],
            &mut frame,
        );
        frame
    }

    #[test]
    fn finds_the_ticket_deep_in_the_first_frame() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);

        assert_eq!(handover.take_from(&first_frame(TICKET), now), Some(server()));
    }

    #[test]
    fn takes_a_ticket_once() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);
        handover.take_from(&first_frame(TICKET), now);

        assert_eq!(handover.take_from(&first_frame(TICKET), now), None);
        assert_eq!(handover.len(), 0);
    }

    #[test]
    fn a_ticket_expires() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);

        let late = handover.take_from(&first_frame(TICKET), now + Handover::LIFETIME);

        assert_eq!(late, None);
        assert_eq!(handover.len(), 0);
    }

    #[test]
    fn a_ticket_is_still_there_just_before_it_expires() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);

        let found = handover.take_from(&first_frame(TICKET), now + Duration::from_millis(59_999));

        assert_eq!(found, Some(server()));
    }

    #[test]
    fn an_unknown_ticket_or_a_frame_that_is_not_protobuf_finds_nothing() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);

        assert_eq!(handover.take_from(&first_frame(OTHER), now), None);
        assert_eq!(handover.take_from(&[0xff, 0xff], now), None);
        assert_eq!(handover.len(), 1);
    }

    #[test]
    fn several_tickets_wait_apart() {
        let mut handover = Handover::default();
        let now = Instant::now();
        let other = GameServer {
            host: "other.example.com".into(),
            port: 443,
        };
        handover.put(ticket(TICKET), server(), now);
        handover.put(ticket(OTHER), other.clone(), now);

        assert_eq!(handover.take_from(&first_frame(OTHER), now), Some(other));
        assert_eq!(handover.take_from(&first_frame(TICKET), now), Some(server()));
    }

    #[test]
    fn looks_down_to_its_depth_and_no_further() {
        let mut handover = Handover::default();
        let now = Instant::now();
        handover.put(ticket(TICKET), server(), now);

        assert_eq!(handover.take_from(&nested(TICKET, Handover::DEPTH + 1), now), None);
        assert_eq!(
            handover.take_from(&nested(TICKET, Handover::DEPTH), now),
            Some(server())
        );
    }

    #[test]
    fn refuses_what_is_not_a_ticket() {
        assert!(Ticket::new(b"").is_none());
        assert!(Ticket::new(b"0123456789abcdef").is_none());
        assert!(Ticket::new(b"0123456789abcdef0123456789abcde!").is_none());
        assert!(Ticket::new(TICKET).is_some());
    }

    #[test]
    fn debug_never_shows_the_ticket() {
        let mut handover = Handover::default();
        handover.put(ticket(TICKET), server(), Instant::now());

        let shown = format!("{handover:?}");

        assert!(!shown.contains("0123456789abcdef"), "{shown}");
        assert!(shown.contains("<redacted>"), "{shown}");
    }
}
