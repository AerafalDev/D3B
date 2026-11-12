//! The game connection through the relay (`docs/relay.md`, "The connections"; inferred, `main:docs/client-game.md`,
//! "The game connection", 3.7.12.12 and 3.7.13.17). The client speaks first, a frame holding its ticket; the relay
//! finds where that ticket's game goes, connects there, relays the frame first, then every byte both ways untouched,
//! splitting each way into frames for the protocol as it copies (`docs/protocol.md`, "The tap").

use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};
use tokio::net::TcpListener;
use tokio::task::JoinSet;
use tokio::time::Instant;

use super::capture::{Recorder, Recording};
use super::connection_phase::connect;
use super::frame::{self, Frames};
use super::handover::{GameServer, Handover, Waiting};

/// How long the client's first frame may take to come whole; it speaks first, at once (inferred, 3.7.12.12).
const FIRST_FRAME: Duration = Duration::from_secs(5);
/// The most the first frame may hold; it is 76 bytes (inferred, 3.7.12.12).
const FIRST_FRAME_LIMIT: usize = 64 * 1024;

/// How much one read may take: a game server's frames are up to 88,581 bytes (verified, 3.7.13.17), most far smaller.
const READ: usize = 64 * 1024;

/// Which side sent a frame.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Side {
    Client,
    Server,
}

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
/// With `recording`, each game connection is also recorded there (`D3B_RECORD`, `docs/protocol.md`).
pub(crate) async fn serve(
    listener: TcpListener,
    handover: Arc<Mutex<Handover>>,
    recording: Option<Recording>,
    report: impl Fn(Game) + Clone + Send + Sync + 'static,
) {
    let recording = recording.map(Arc::new);
    let mut connections = JoinSet::new();
    let mut accepted: u64 = 0;
    loop {
        let Ok((client, _)) = listener.accept().await else {
            // Out of sockets, say: wait rather than spin.
            tokio::time::sleep(Duration::from_millis(100)).await;
            continue;
        };
        let _ = client.set_nodelay(true);
        let handover = Arc::clone(&handover);
        let report = report.clone();
        accepted += 1;
        let number = accepted;
        let recording = recording.clone();
        connections.spawn(async move {
            let opened = {
                let report = report.clone();
                move || report(Game::Opened)
            };
            let connect = |server: GameServer| async move { connect(&server.host, server.port).await.ok() };
            let record = |waiting: &Waiting| {
                let build = waiting.build.as_deref();
                recording
                    .as_deref()
                    .map(|recording| recording.start(&waiting.server, build, std::time::SystemTime::now(), number))
            };
            // The protocol will read the frames here (docs/protocol.md); until then they are only split.
            let relayed = relay(client, &handover, connect, opened, record, &|_, _| {}).await;
            report(if relayed { Game::Closed } else { Game::Refused });
        });
        while connections.try_join_next().is_some() {}
    }
}

/// Relays one game connection: reads the client's first frame, takes its ticket's game server from `handover`, reaches
/// it through `connect`, says so through `opened`, relays the bytes read so far, then both ways until both sides are
/// done, giving `tap` the body of every frame each side sent, the first one included, and the recorder `record` makes
/// for what waited for the ticket, if any, every read. `false` when it closed before reaching a game server.
pub(crate) async fn relay<C, S, F>(
    mut client: C,
    handover: &Mutex<Handover>,
    connect: impl FnOnce(GameServer) -> F,
    opened: impl FnOnce(),
    record: impl FnOnce(&Waiting) -> Option<Recorder>,
    tap: &(impl Fn(Side, &[u8]) + Sync),
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
    let waiting = handover
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        .take_from(&read[header..header + body], Instant::now().into_std());
    let Some(waiting) = waiting else {
        return false;
    };
    let Some(mut server) = connect(waiting.server.clone()).await else {
        return false;
    };
    opened();
    let recorder = record(&waiting);
    let note = |side, bytes: &[u8]| {
        if let Some(recorder) = &recorder {
            recorder.record(side, bytes);
        }
    };
    note(Side::Client, &read);
    if server.write_all(&read).await.is_ok() {
        let mut from_client = Some(Frames::default());
        tapped(&mut from_client, &read, &|body| tap(Side::Client, body));
        let (client_read, client_write) = tokio::io::split(client);
        let (server_read, server_write) = tokio::io::split(server);
        // Both ways at once, the first error ending both, as `copy_bidirectional` did.
        let _ = tokio::try_join!(
            pump(
                client_read,
                server_write,
                from_client,
                |bytes| note(Side::Client, bytes),
                |body| tap(Side::Client, body)
            ),
            pump(
                server_read,
                client_write,
                Some(Frames::default()),
                |bytes| note(Side::Server, bytes),
                |body| tap(Side::Server, body)
            ),
        );
    }
    if let Some(recorder) = recorder {
        recorder.finish().await;
    }
    true
}

/// Copies `from` to `to` read by read, each read written before anything else, then given to `record` and split into
/// frames for `tap`; once `from` ends, shuts `to` down and returns, the other way going on. A stream that is no longer
/// in frames stops the frames, never the copy.
async fn pump(
    mut from: impl AsyncRead + Unpin,
    mut to: impl AsyncWrite + Unpin,
    mut frames: Option<Frames>,
    record: impl Fn(&[u8]),
    tap: impl Fn(&[u8]),
) -> std::io::Result<()> {
    let mut buffer = vec![0; READ];
    loop {
        let count = from.read(&mut buffer).await?;
        if count == 0 {
            return to.shutdown().await;
        }
        to.write_all(&buffer[..count]).await?;
        to.flush().await?;
        record(&buffer[..count]);
        tapped(&mut frames, &buffer[..count], &tap);
    }
}

/// Gives `tap` the frames `bytes` completes; drops the reader at its first error, so that it gives nothing more.
fn tapped(frames: &mut Option<Frames>, bytes: &[u8], tap: &impl Fn(&[u8])) {
    if let Some(reader) = frames
        && reader.push(bytes, tap).is_err()
    {
        *frames = None;
    }
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
            Waiting {
                server: GameServer {
                    host: "game.example.com".into(),
                    port: 5555,
                },
                build: Some("3.7.13.17".into()),
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
        game_reaching(handover, true, Arc::default())
    }

    /// The frames the relay tapped, in order.
    type Tapped = Arc<Mutex<Vec<(Side, Vec<u8>)>>>;

    /// As [`game`], with a game server that can be reached or not and the frames tapped kept in `tapped`; the relay's
    /// result comes with how many times it said the connection opened.
    fn game_reaching(handover: Mutex<Handover>, reachable: bool, tapped: Tapped) -> Relayed {
        let (client, client_side) = tokio::io::duplex(1 << 16);
        let (connected, server) = tokio::sync::oneshot::channel();
        let relay = tokio::spawn(async move {
            let tap = move |side, body: &[u8]| {
                tapped
                    .lock()
                    .unwrap_or_else(std::sync::PoisonError::into_inner)
                    .push((side, body.to_vec()));
            };
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
                |_| None,
                &tap,
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

    /// Reads exactly `count` bytes from `stream`, within a second, so that a byte lost fails the test rather than hangs
    /// it.
    async fn received(stream: &mut DuplexStream, count: usize) -> Vec<u8> {
        let mut bytes = vec![0; count];
        let read = tokio::time::timeout(Duration::from_secs(1), stream.read_exact(&mut bytes)).await;
        assert!(matches!(read, Ok(Ok(_))), "{read:?}");
        bytes
    }

    fn frames_of(bodies: &[&[u8]]) -> Vec<u8> {
        let mut bytes = Vec::new();
        for body in bodies {
            frame::encode(body, &mut bytes);
        }
        bytes
    }

    fn tapped_from(tapped: &Tapped, side: Side) -> Vec<Vec<u8>> {
        tapped
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .iter()
            .filter(|(from, _)| *from == side)
            .map(|(_, body)| body.clone())
            .collect()
    }

    #[tokio::test(start_paused = true)]
    async fn taps_every_frame_both_ways_as_it_copies() {
        let tapped = Tapped::default();
        let (mut client, server, relay) = game_reaching(handover_waiting(), true, Arc::clone(&tapped));
        let first = first(TICKET);
        let large: Vec<u8> = (0..88_581_u32).map(|index| (index % 251) as u8).collect();
        let from_server = frames_of(&[b"accepted", &large, b""]);
        let from_client = frames_of(&[b"list", b"choose"]);

        client.write_all(&first).await.unwrap_or_else(|error| panic!("{error}"));
        let (_, mut server) = server.await.unwrap_or_else(|error| panic!("{error}"));
        let at_server = received(&mut server, first.len()).await;
        // The server's bytes in writes that cut frames anywhere, the client's in one.
        let reading = tokio::spawn(async move {
            let at_client = received(&mut client, 8 + 1 + 88_581 + 3 + 1).await;
            (client, at_client)
        });
        for chunk in from_server.chunks(1_000) {
            server.write_all(chunk).await.unwrap_or_else(|error| panic!("{error}"));
        }
        let (mut client, at_client) = reading.await.unwrap_or_else(|error| panic!("{error}"));
        client
            .write_all(&from_client)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let rest = received(&mut server, from_client.len()).await;
        drop(client);
        drop(server);

        assert_eq!(relay.await.ok(), Some((true, 1)));
        assert_eq!(at_server, first);
        assert_eq!(at_client, from_server);
        assert_eq!(rest, from_client);
        let header = frame::header(&first).ok().flatten().map_or(0, |(header, _)| header);
        assert_eq!(
            tapped_from(&tapped, Side::Client),
            vec![first[header..].to_vec(), b"list".to_vec(), b"choose".to_vec()]
        );
        assert_eq!(
            tapped_from(&tapped, Side::Server),
            vec![b"accepted".to_vec(), large, Vec::new()]
        );
    }

    #[tokio::test(start_paused = true)]
    async fn a_stream_no_longer_in_frames_stops_the_tap_not_the_copy() {
        let tapped = Tapped::default();
        let (mut client, server, relay) = game_reaching(handover_waiting(), true, Arc::clone(&tapped));
        let first = first(TICKET);
        let mut from_server = frames_of(&[b"accepted"]);
        from_server.extend_from_slice(&[0xff; 6]);
        from_server.extend_from_slice(&frames_of(&[b"after"]));

        client.write_all(&first).await.unwrap_or_else(|error| panic!("{error}"));
        let (_, mut server) = server.await.unwrap_or_else(|error| panic!("{error}"));
        received(&mut server, first.len()).await;
        server
            .write_all(&from_server)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let at_client = received(&mut client, from_server.len()).await;
        server
            .write_all(&frames_of(&[b"later"]))
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let later = received(&mut client, 6).await;
        client
            .write_all(&frames_of(&[b"still"]))
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let still = received(&mut server, 6).await;
        drop(client);
        drop(server);

        assert_eq!(relay.await.ok(), Some((true, 1)));
        assert_eq!(at_client, from_server);
        assert_eq!(later, frames_of(&[b"later"]));
        assert_eq!(still, frames_of(&[b"still"]));
        assert_eq!(tapped_from(&tapped, Side::Server), vec![b"accepted".to_vec()]);
        assert_eq!(tapped_from(&tapped, Side::Client).len(), 2);
    }

    #[tokio::test]
    async fn writes_each_read_before_the_tap_sees_it() {
        let (mut from, from_side) = tokio::io::duplex(1 << 16);
        let (to_side, mut to) = tokio::io::duplex(1 << 16);
        let frame = frames_of(&[b"read"]);
        from.write_all(&frame).await.unwrap_or_else(|error| panic!("{error}"));

        // A tap that fails on the first frame: the bytes must already be on their way.
        let pumped = tokio::spawn(pump(
            from_side,
            to_side,
            Some(Frames::default()),
            |_| {},
            |_| panic!("tapped"),
        ))
        .await;

        assert!(pumped.is_err(), "the tap was not reached");
        assert_eq!(received(&mut to, frame.len()).await, frame);
    }

    #[tokio::test]
    async fn records_every_read_of_both_sides_the_first_included() {
        let folder = std::env::temp_dir().join(format!("d3b-game-capture-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&folder);
        let recording = Recording { folder: folder.clone() };
        let handover = handover_waiting();
        let (mut client, client_side) = tokio::io::duplex(1 << 16);
        let (mut server, server_side) = tokio::io::duplex(1 << 16);
        let first = first(TICKET);
        let from_server = frames_of(&[b"accepted"]);
        let from_client = frames_of(&[b"list"]);

        let relay = tokio::spawn(async move {
            relay(
                client_side,
                &handover,
                |_| async move { Some(server_side) },
                || {},
                |waiting: &Waiting| {
                    let build = waiting.build.as_deref();
                    Some(recording.start(&waiting.server, build, std::time::SystemTime::UNIX_EPOCH, 1))
                },
                &|_, _| {},
            )
            .await
        });
        client.write_all(&first).await.unwrap_or_else(|error| panic!("{error}"));
        received(&mut server, first.len()).await;
        server
            .write_all(&from_server)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        received(&mut client, from_server.len()).await;
        client
            .write_all(&from_client)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        received(&mut server, from_client.len()).await;
        drop(client);
        drop(server);
        let relayed = relay.await.ok();

        let path = folder.join("3.7.13.17-0-1.d3bcap");
        let capture = super::super::capture::read(&std::fs::read(&path).unwrap_or_default());
        let _ = std::fs::remove_dir_all(&folder);
        assert_eq!(relayed, Some(true));
        let records = capture.unwrap_or_else(|error| panic!("{error}")).records;
        let side = |client: bool| -> Vec<u8> {
            records
                .iter()
                .filter_map(|record| match (record, client) {
                    (super::super::capture::Record::FromClient(bytes), true)
                    | (super::super::capture::Record::FromServer(bytes), false) => Some(bytes.clone()),
                    _ => None,
                })
                .flatten()
                .collect()
        };
        assert_eq!(side(true), [first, from_client].concat());
        assert_eq!(side(false), from_server);
    }

    #[tokio::test(start_paused = true)]
    async fn a_side_that_ends_ends_its_way_only() {
        let (mut client, server, relay) = game(handover_waiting());
        let first = first(TICKET);

        client.write_all(&first).await.unwrap_or_else(|error| panic!("{error}"));
        let (_, mut server) = server.await.unwrap_or_else(|error| panic!("{error}"));
        received(&mut server, first.len()).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut rest = Vec::new();
        let ended = tokio::time::timeout(Duration::from_secs(1), client.read_to_end(&mut rest)).await;
        client
            .write_all(&frames_of(&[b"after"]))
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        let after = received(&mut server, 6).await;
        let open = !relay.is_finished();
        client.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut last = Vec::new();
        let closed = tokio::time::timeout(Duration::from_secs(1), server.read_to_end(&mut last)).await;

        assert!(matches!(ended, Ok(Ok(0))), "{ended:?}");
        assert_eq!(after, frames_of(&[b"after"]));
        assert!(open, "the client's way ended with the server's");
        assert!(matches!(closed, Ok(Ok(0))), "{closed:?}");
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
        let (mut client, _server, relay) = game_reaching(handover_waiting(), false, Arc::default());

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
