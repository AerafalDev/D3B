//! The game connection through the relay (`docs/relay.md`, "The connections"; inferred, `main:docs/client-game.md`,
//! "The game connection", 3.7.12.12 and 3.7.13.17). The client speaks first, a frame holding its ticket; the relay
//! finds where that ticket's game goes, connects there, relays the frame first, then every byte both ways untouched.

use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};
use tokio::net::TcpListener;
use tokio::task::JoinSet;
use tokio::time::Instant;

use super::connection_phase::connect;
use super::frame;
use super::handover::{GameServer, Handover};

/// How long the client's first frame may take to come whole; it speaks first, at once (inferred, 3.7.12.12).
const FIRST_FRAME: Duration = Duration::from_secs(5);
/// The most the first frame may hold; it is 76 bytes (inferred, 3.7.12.12).
const FIRST_FRAME_LIMIT: usize = 64 * 1024;

/// What happened to a game connection, for the relay's status.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Game {
    /// Relayed to its game server: one more game connection.
    Opened,
    /// A relayed game connection ended.
    Closed,
    /// Closed before reaching a game server: no whole first frame in time, no waiting ticket in it, or the game
    /// server could not be reached.
    Refused,
}

/// Accepts game connections on `listener` until the task is aborted; its connections end with it.
pub(crate) async fn serve(
    listener: TcpListener,
    handover: Arc<Mutex<Handover>>,
    report: impl Fn(Game) + Clone + Send + Sync + 'static,
) {
    let mut connections = JoinSet::new();
    loop {
        let Ok((client, _)) = listener.accept().await else {
            // Out of sockets, say: wait rather than spin.
            tokio::time::sleep(Duration::from_millis(100)).await;
            continue;
        };
        let _ = client.set_nodelay(true);
        let handover = Arc::clone(&handover);
        let report = report.clone();
        connections.spawn(async move {
            let opened = {
                let report = report.clone();
                move || report(Game::Opened)
            };
            let connect = |server: GameServer| async move { connect(&server.host, server.port).await.ok() };
            let relayed = relay(client, &handover, connect, opened).await;
            report(if relayed { Game::Closed } else { Game::Refused });
        });
        while connections.try_join_next().is_some() {}
    }
}

/// Relays one game connection: reads the client's first frame, takes its ticket's game server from `handover`,
/// reaches it through `connect`, says so through `opened`, relays the bytes read so far, then both ways until both
/// sides are done. `false` when it closed before reaching a game server.
pub(crate) async fn relay<C, S, F>(
    mut client: C,
    handover: &Mutex<Handover>,
    connect: impl FnOnce(GameServer) -> F,
    opened: impl FnOnce(),
) -> bool
where
    C: AsyncRead + AsyncWrite + Unpin,
    S: AsyncRead + AsyncWrite + Unpin,
    F: Future<Output = Option<S>>,
{
    let Some(read) = first_frame(&mut client).await else {
        return false;
    };
    let Some((header, body)) = frame::header(&read).ok().flatten() else {
        return false;
    };
    let server = handover
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        .take_from(&read[header..header + body], Instant::now().into_std());
    let Some(server) = server else {
        return false;
    };
    let Some(mut server) = connect(server).await else {
        return false;
    };
    opened();
    if server.write_all(&read).await.is_err() {
        return true;
    }
    let _ = tokio::io::copy_bidirectional(&mut client, &mut server).await;
    true
}

/// The bytes the client sent until its first frame is whole, within [`FIRST_FRAME`]; `None` when it is not, or is
/// not a frame, or is larger than [`FIRST_FRAME_LIMIT`].
async fn first_frame(client: &mut (impl AsyncRead + Unpin)) -> Option<Vec<u8>> {
    let mut read = Vec::new();
    let mut chunk = vec![0; 8192];
    let deadline = Instant::now() + FIRST_FRAME;
    loop {
        match frame::header(&read) {
            Ok(Some((header, body))) if body <= FIRST_FRAME_LIMIT => {
                if read.len() >= header + body {
                    return Some(read);
                }
            }
            Ok(None) => {}
            Ok(Some(_)) | Err(_) => return None,
        }
        let count = tokio::time::timeout_at(deadline, client.read(&mut chunk))
            .await
            .ok()?
            .ok()?;
        if count == 0 {
            return None;
        }
        read.extend_from_slice(&chunk[..count]);
    }
}

#[cfg(test)]
mod tests {
    use tokio::io::DuplexStream;

    use super::super::handover::Ticket;
    use super::super::wire::{self, Field, Value};
    use super::*;

    const TICKET: &[u8] = b"0123456789abcdef0123456789abcdef";

    fn handover_waiting() -> Mutex<Handover> {
        let mut handover = Handover::default();
        let ticket = Ticket::new(TICKET).unwrap_or_else(|| panic!("not a ticket"));
        handover.put(
            ticket,
            GameServer {
                host: "game.example.com".into(),
                port: 5555,
            },
            Instant::now().into_std(),
        );
        Mutex::new(handover)
    }

    /// The client's first game frame: `request = 4 { id = 1, Any = 2 { url = 1, value = 2 { ticket = 1, language = 2
    /// } } }` (inferred, `main:docs/client-game.md`, 3.7.13.17).
    fn first(ticket: &[u8]) -> Vec<u8> {
        let message = |fields: &[Field<'_>]| {
            let mut out = Vec::new();
            wire::encode(fields, &mut out);
            out
        };
        let authentication = message(&[
            Field {
                number: 1,
                value: Value::Bytes(ticket),
            },
            Field {
                number: 2,
                value: Value::Bytes(b"fr"),
            },
        ]);
        let any = message(&[
            Field {
                number: 1,
                value: Value::Bytes(b"type.ankama.com/abc"),
            },
            Field {
                number: 2,
                value: Value::Bytes(&authentication),
            },
        ]);
        let request = message(&[
            Field {
                number: 1,
                value: Value::Varint(u64::MAX),
            },
            Field {
                number: 2,
                value: Value::Bytes(&any),
            },
        ]);
        let mut frame = Vec::new();
        frame::encode(
            &message(&[Field {
                number: 4,
                value: Value::Bytes(&request),
            }]),
            &mut frame,
        );
        frame
    }

    /// The client's end, the game server's end once the relay connected (with where it connected), and the relay's
    /// result with how many times it said the connection opened.
    type Relayed = (
        DuplexStream,
        tokio::sync::oneshot::Receiver<(GameServer, DuplexStream)>,
        tokio::task::JoinHandle<(bool, usize)>,
    );

    /// Relays a game connection over in-memory streams: the client's end, the game server's end (once connected) and
    /// the relay's result; `connected` says where the relay connected.
    fn game(handover: Mutex<Handover>) -> Relayed {
        game_reaching(handover, true)
    }

    /// As [`game`], with a game server that can be reached or not; the relay's result comes with how many times it
    /// said the connection opened.
    fn game_reaching(handover: Mutex<Handover>, reachable: bool) -> Relayed {
        let (client, client_side) = tokio::io::duplex(1 << 16);
        let (connected, server) = tokio::sync::oneshot::channel();
        let relay = tokio::spawn(async move {
            let mut opened = 0;
            let relayed = relay(
                client_side,
                &handover,
                |game| async move {
                    if !reachable {
                        return None;
                    }
                    let (server, server_side) = tokio::io::duplex(1 << 16);
                    let _ = connected.send((game, server));
                    Some(server_side)
                },
                || opened += 1,
            )
            .await;
            (relayed, opened)
        });
        (client, server, relay)
    }

    #[tokio::test(start_paused = true)]
    async fn relays_the_first_frame_then_both_ways() {
        let (mut client, server, relay) = game(handover_waiting());
        let mut first = first(TICKET);
        // A second frame in the same read, as the client may send it at once.
        first.extend_from_slice(b"\x02hi");

        // Cut in two, as the network may cut it.
        client
            .write_all(&first[..20])
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        tokio::task::yield_now().await;
        client
            .write_all(&first[20..])
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let (to, mut server) = server.await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = vec![0; first.len()];
        // Bounded, so that a byte lost fails the test rather than hangs it.
        let read = tokio::time::timeout(Duration::from_secs(1), server.read_exact(&mut received)).await;
        assert!(matches!(read, Ok(Ok(_))), "{read:?}");
        server
            .write_all(b"from the game")
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let mut answer = [0; 13];
        client
            .read_exact(&mut answer)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);
        drop(server);

        assert_eq!(
            to,
            GameServer {
                host: "game.example.com".into(),
                port: 5555
            }
        );
        assert_eq!(received, first);
        assert_eq!(&answer, b"from the game");
        assert_eq!(relay.await.ok(), Some((true, 1)));
    }

    #[tokio::test]
    async fn closes_a_connection_with_no_waiting_ticket() {
        let (mut client, server, relay) = game(handover_waiting());

        client
            .write_all(&first(b"fedcba9876543210fedcba9876543210"))
            .await
            .unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(relay.await.ok(), Some((false, 0)));
        assert!(server.await.is_err(), "it connected somewhere");
        let mut rest = Vec::new();
        assert_eq!(client.read_to_end(&mut rest).await.ok(), Some(0));
    }

    #[tokio::test]
    async fn a_ticket_is_taken_once() {
        let handover = handover_waiting();
        let first = first(TICKET);
        let taken = handover
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .take_from(&first[1..], Instant::now().into_std());
        assert!(taken.is_some());
        let (mut client, _server, relay) = game(handover);

        client.write_all(&first).await.unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(relay.await.ok(), Some((false, 0)));
    }

    #[tokio::test(start_paused = true)]
    async fn closes_a_connection_whose_first_frame_does_not_come_in_time() {
        let (mut client, _server, relay) = game(handover_waiting());
        let first = first(TICKET);

        client
            .write_all(&first[..10])
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        // Five seconds, as docs/relay.md says, written out so that the test pins it.
        tokio::time::sleep(Duration::from_millis(4_999)).await;
        assert!(!relay.is_finished(), "closed before five seconds");
        tokio::time::sleep(Duration::from_millis(2)).await;
        assert!(relay.is_finished(), "still open after five seconds");

        assert_eq!(relay.await.ok(), Some((false, 0)));
    }

    #[tokio::test(start_paused = true)]
    async fn closes_a_connection_whose_game_server_cannot_be_reached() {
        let (mut client, _server, relay) = game_reaching(handover_waiting(), false);

        client
            .write_all(&first(TICKET))
            .await
            .unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(relay.await.ok(), Some((false, 0)));
        let mut rest = Vec::new();
        assert_eq!(client.read_to_end(&mut rest).await.ok(), Some(0));
    }

    #[tokio::test]
    async fn accepts_a_first_frame_of_exactly_the_limit() {
        let mut bytes = Vec::new();
        wire::write_varint(FIRST_FRAME_LIMIT as u64, &mut bytes);
        bytes.resize(bytes.len() + FIRST_FRAME_LIMIT, 0);
        let (mut client, mut relay_side) = tokio::io::duplex(1 << 20);

        client.write_all(&bytes).await.unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(
            first_frame(&mut relay_side).await.map(|read| read.len()),
            Some(bytes.len())
        );
    }

    #[tokio::test(start_paused = true)]
    async fn closes_what_is_not_a_frame() {
        let (mut client, _server, relay) = game(handover_waiting());

        client
            .write_all(&[0xff; 6])
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        // At once, not when the first frame's time is up.
        tokio::time::sleep(Duration::from_millis(1)).await;

        assert!(relay.is_finished());
        assert_eq!(relay.await.ok(), Some((false, 0)));
    }

    #[tokio::test(start_paused = true)]
    async fn closes_a_first_frame_too_large() {
        let (mut client, _server, relay) = game(handover_waiting());
        let mut header = Vec::new();
        wire::write_varint(FIRST_FRAME_LIMIT as u64 + 1, &mut header);

        client
            .write_all(&header)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        // At once, not when the first frame's time is up.
        tokio::time::sleep(Duration::from_millis(1)).await;

        assert!(relay.is_finished());
        assert_eq!(relay.await.ok(), Some((false, 0)));
    }
}
