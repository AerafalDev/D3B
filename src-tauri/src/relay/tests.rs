//! The owner's captures replayed through the relay (`docs/relay.md`, "Tests"). They are the C# version's `.d3bcap`
//! files, format 1 or 2 (inferred, `main:src/D3B.Network/Captures/CaptureLayout.cs`), which record each side's bytes as
//! one read gave them, before any rewrite (`main:src/D3B.Network/Relay/RelayConnectionHandler.cs`, `PumpAsync`). They
//! hold the account's login token and tickets: a failure names the file, a frame's index and lengths, never a byte of
//! content. Ignored unless run with `D3B_CAPTURES` naming their folder.

use std::path::{Path, PathBuf};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use tokio::io::{AsyncReadExt, AsyncWriteExt, DuplexStream};

use super::connection_phase::{self, Handed};
use super::handover::{GameServer, Handover, Ticket};
use super::wire::{self, Field, Value};
use super::{ADDRESS, GAME_PORT, frame, game_connection};

/// The most one record holds, as the C# reader bounds it: only a damaged file reaches it.
const RECORD_LIMIT: u64 = 64 * 1024 * 1024;

/// One capture's records, in order.
struct Capture {
    records: Vec<Record>,
}

#[derive(PartialEq, Eq)]
enum Record {
    FromClient(Vec<u8>),
    FromServer(Vec<u8>),
    /// A side closed, or a frame the C# bot sent itself: nothing to replay.
    Other,
}

/// A 7-bit length at the start of `bytes` and the bytes it took, bounded by [`RECORD_LIMIT`].
fn length(bytes: &[u8]) -> Result<Option<(usize, usize)>, String> {
    let Some((length, used)) = wire::read_varint(bytes) else {
        return Ok(None);
    };
    if length > RECORD_LIMIT {
        return Err(format!("a length of {length}, more than any record holds"));
    }
    Ok(usize::try_from(length).ok().map(|length| (length, used)))
}

/// Reads a capture: the magic `D3B-CAPT`, the format as a little-endian `i32`, the start as a little-endian `i64`,
/// four strings after their 7-bit lengths (client version, listener, server host, server), then records of a kind
/// byte, the microseconds since the start as a 7-bit number, and for bytes a 7-bit length and the bytes. A record cut
/// short ends the capture, as a crash leaves it.
fn read(bytes: &[u8]) -> Result<Capture, String> {
    let rest = bytes.strip_prefix(b"D3B-CAPT").ok_or("not a D3B capture")?;
    let (format, rest) = rest.split_first_chunk::<4>().ok_or("cut in its header")?;
    let format = i32::from_le_bytes(*format);
    if !matches!(format, 1 | 2) {
        return Err(format!("format {format}"));
    }
    let (_, mut rest) = rest.split_first_chunk::<8>().ok_or("cut in its header")?;
    for _ in 0..4 {
        let (length, used) = length(rest)?.ok_or("cut in its header")?;
        let end = used
            .checked_add(length)
            .filter(|&end| end <= rest.len())
            .ok_or("cut in its header")?;
        rest = &rest[end..];
    }
    let mut records = Vec::new();
    while let Some((&kind, tail)) = rest.split_first() {
        let Some((_, used)) = wire::read_varint(tail) else {
            break;
        };
        let mut tail = &tail[used..];
        let record = match kind {
            1 | 2 | 5 => {
                let Some((length, used)) = length(tail)? else { break };
                let Some(data) = used.checked_add(length).and_then(|end| tail.get(used..end)) else {
                    break;
                };
                tail = &tail[used + data.len()..];
                match kind {
                    1 => Record::FromClient(data.to_vec()),
                    2 => Record::FromServer(data.to_vec()),
                    _ => Record::Other,
                }
            }
            3 | 4 => Record::Other,
            other => return Err(format!("a record of kind {other}")),
        };
        records.push(record);
        rest = tail;
    }
    Ok(Capture { records })
}

fn joined(records: &[Record], client: bool) -> Vec<u8> {
    let mut bytes = Vec::new();
    for record in records {
        match (record, client) {
            (Record::FromClient(data), true) | (Record::FromServer(data), false) => bytes.extend_from_slice(data),
            _ => {}
        }
    }
    bytes
}

/// The frames of `bytes`, and the length of what follows the last whole one.
fn frames(mut bytes: &[u8]) -> (Vec<&[u8]>, usize) {
    let mut frames = Vec::new();
    while let Ok(Some(total)) = frame::whole(bytes) {
        frames.push(&bytes[..total]);
        bytes = &bytes[total..];
    }
    (frames, bytes.len())
}

fn body(frame: &[u8]) -> &[u8] {
    let (header, body) = frame::header(frame).ok().flatten().unwrap_or_default();
    frame.get(header..header + body).unwrap_or_default()
}

/// The fields of `message`, as far as they read.
fn fields(message: &[u8]) -> Vec<Field<'_>> {
    wire::fields(message).map_while(Result::ok).collect()
}

/// The value of the length-delimited field `number` of `message`.
fn bytes(message: &[u8], number: u32) -> Option<&[u8]> {
    fields(message).into_iter().find_map(|field| match field.value {
        Value::Bytes(bytes) if field.number == number => Some(bytes),
        _ => None,
    })
}

/// The value at `path` of `message`, through length-delimited fields.
fn at<'a>(message: &'a [u8], path: &[u32]) -> Option<&'a [u8]> {
    path.iter().try_fold(message, |message, &number| bytes(message, number))
}

/// Whether the client chose a server, read by the test itself: a frame holding `request = 1 { server_selection = 4 }`.
fn chose_a_server(from_client: &[u8]) -> bool {
    frames(from_client)
        .0
        .iter()
        .any(|frame| at(body(frame), &[1, 4]).is_some())
}

fn ports(packed: &[u8]) -> Vec<u64> {
    let mut ports = Vec::new();
    let mut rest = packed;
    while let Some((port, used)) = wire::read_varint(rest) {
        ports.push(port);
        rest = &rest[used..];
    }
    ports
}

/// The fields of a server frame's `response`, but its `server_selection`.
fn others(message: &[u8]) -> Vec<Field<'_>> {
    fields(at(message, &[2]).unwrap_or_default())
        .into_iter()
        .filter(|field| field.number != 4)
        .collect()
}

/// Whether `got` is `sent`, the selection's answer, with the relay's address and game port in place of Ankama's,
/// every other field kept; and the handover holds Ankama's host and first port under its ticket.
fn rewritten_well(sent: &[u8], got: &[u8], handover: &Mutex<Handover>) -> bool {
    let (sent, got) = (body(sent), body(got));
    let (Some(sent_success), Some(got_success)) = (at(sent, &[2, 4, 1]), at(got, &[2, 4, 1])) else {
        return false;
    };
    let (Some(ticket), Some(host), Some(sent_ports)) =
        (bytes(sent_success, 1), bytes(sent_success, 2), bytes(sent_success, 3))
    else {
        return false;
    };
    let relay_host = ADDRESS.to_string();
    let first = ports(sent_ports)
        .first()
        .copied()
        .and_then(|port| u16::try_from(port).ok());
    let mut presented = Vec::new();
    wire::encode(
        &[Field {
            number: 1,
            value: Value::Bytes(ticket),
        }],
        &mut presented,
    );
    let waiting = handover
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        .take_from(&presented, Instant::now());
    fields(got).len() == 1
        && fields(at(got, &[2, 4]).unwrap_or_default()).len() == 1
        && others(sent) == others(got)
        && bytes(got_success, 1) == Some(ticket)
        && bytes(got_success, 2) == Some(relay_host.as_bytes())
        && bytes(got_success, 3).map(ports) == Some(vec![u64::from(GAME_PORT)])
        && fields(got_success).len() == 3
        && waiting
            == first.map(|port| GameServer {
                host: String::from_utf8_lossy(host).into_owned(),
                port,
            })
}

/// Plays `records` on the client's and the server's ends, each client read waited for at the server's end, so that
/// the relay sees them in their recorded order; then closes both and returns what each end received. A wait longer
/// than a second (of paused time) gives up, so that a byte held fails the replay rather than hangs it.
async fn play(records: &[Record], mut client: DuplexStream, mut server: DuplexStream) -> (Vec<u8>, Vec<u8>) {
    let (mut client_read, mut client_write) = tokio::io::split(&mut client);
    let (mut server_read, mut server_write) = tokio::io::split(&mut server);
    let mut at_server = Vec::new();
    let to_client = async {
        let mut received = Vec::new();
        let _ = client_read.read_to_end(&mut received).await;
        received
    };
    let feed = async {
        for record in records {
            match record {
                Record::FromClient(data) => {
                    if client_write.write_all(data).await.is_err() {
                        break;
                    }
                    let mut forwarded = vec![0; data.len()];
                    let read = tokio::time::timeout(Duration::from_secs(1), server_read.read_exact(&mut forwarded));
                    if !matches!(read.await, Ok(Ok(_))) {
                        break;
                    }
                    at_server.extend_from_slice(&forwarded);
                }
                Record::FromServer(data) => {
                    if server_write.write_all(data).await.is_err() {
                        break;
                    }
                }
                Record::Other => {}
            }
        }
        let _ = server_write.shutdown().await;
        let _ = client_write.shutdown().await;
        let _ = server_read.read_to_end(&mut at_server).await;
    };
    let ((), at_client) = tokio::join!(feed, to_client);
    (at_client, at_server)
}

/// Replays a connection phase: the client's bytes reach the server untouched, and the server's reach the client
/// untouched but for one frame, the selection's answer, rewritten when the client chose a server.
async fn connection_phase(name: &str, capture: &Capture) -> Result<Handed, String> {
    let (client, client_side) = tokio::io::duplex(1 << 20);
    let (server, server_side) = tokio::io::duplex(1 << 20);
    let handover = Mutex::new(Handover::default());
    let relay = connection_phase::relay(client_side, server_side, &handover);
    let (handed, (at_client, at_server)) = tokio::join!(relay, play(&capture.records, client, server));
    let from_client = joined(&capture.records, true);
    if at_server != from_client {
        return Err(format!(
            "{name}: the server got {} bytes of the client's {}",
            at_server.len(),
            from_client.len()
        ));
    }
    let sent = joined(&capture.records, false);
    let (sent_frames, sent_rest) = frames(&sent);
    let (got_frames, got_rest) = frames(&at_client);
    if sent_frames.len() != got_frames.len() || sent_rest != got_rest {
        return Err(format!(
            "{name}: the client got {} frames and {got_rest} bytes, the server sent {} and {sent_rest}",
            got_frames.len(),
            sent_frames.len()
        ));
    }
    let changed: Vec<usize> = (0..sent_frames.len())
        .filter(|&index| sent_frames[index] != got_frames[index])
        .collect();
    // The captures are of builds whose answer the relay reads (3.7.12.12 and 3.7.13.17): a client that chose a server
    // must be handed over, and one that did not must see every byte as sent.
    match (chose_a_server(&from_client), handed, changed.as_slice()) {
        (true, Handed::Over, [index]) if rewritten_well(sent_frames[*index], got_frames[*index], &handover) => {
            Ok(handed)
        }
        (false, Handed::NotAsked, []) => Ok(handed),
        (chose, handed, changed) => Err(format!(
            "{name}: chose a server {chose}, {handed:?}, frames {changed:?} changed"
        )),
    }
}

/// The ticket of a game connection's first frame, at path 4.2.2.1 (inferred, `main:docs/client-game.md`, 3.7.13.17).
fn first_ticket(from_client: &[u8]) -> Option<Ticket> {
    let first = frames(from_client).0.into_iter().next()?;
    Ticket::new(at(body(first), &[4, 2, 2, 1])?)
}

/// The frames a game connection's tap gave, each side's encoded back in order, and how many.
#[derive(Default)]
struct Tapped {
    client: Vec<u8>,
    server: Vec<u8>,
    frames: usize,
}

/// Replays a game connection, its ticket seeded into the handover: every byte reaches the other end untouched, and
/// the tap gives every whole frame each side sent, in order. Returns how many frames it gave.
async fn game(name: &str, capture: &Capture, ticket: Ticket) -> Result<usize, String> {
    let from_client = joined(&capture.records, true);
    let mut handover = Handover::default();
    handover.put(
        ticket,
        GameServer {
            host: "game.example.com".into(),
            port: 5555,
        },
        Instant::now(),
    );
    let handover = Mutex::new(handover);
    let (client, client_side) = tokio::io::duplex(1 << 20);
    let (server, server_side) = tokio::io::duplex(1 << 20);
    let tapped = Mutex::new(Tapped::default());
    let tap = |side, body: &[u8]| {
        let mut tapped = tapped.lock().unwrap_or_else(std::sync::PoisonError::into_inner);
        tapped.frames += 1;
        let into = match side {
            game_connection::Side::Client => &mut tapped.client,
            game_connection::Side::Server => &mut tapped.server,
        };
        frame::encode(body, into);
    };
    let relay = game_connection::relay(
        client_side,
        &handover,
        |_| async move { Some(server_side) },
        || {},
        &tap,
    );
    let (relayed, (at_client, at_server)) = tokio::join!(relay, play(&capture.records, client, server));
    let from_server = joined(&capture.records, false);
    if !relayed || at_server != from_client || at_client != from_server {
        return Err(format!(
            "{name}: relayed {relayed}, the server got {} of {} bytes, the client {} of {}",
            at_server.len(),
            from_client.len(),
            at_client.len(),
            from_server.len()
        ));
    }
    let tapped = tapped.into_inner().unwrap_or_else(std::sync::PoisonError::into_inner);
    // What the tap gave is each side's bytes up to its last whole frame, the capture's end cutting the rest.
    for (side, sent, got) in [
        ("client", &from_client, &tapped.client),
        ("server", &from_server, &tapped.server),
    ] {
        let whole = sent.len() - frames(sent).1;
        if got[..] != sent[..whole] {
            return Err(format!(
                "{name}: the tap gave {} bytes of the {side}'s {whole} in whole frames",
                got.len()
            ));
        }
    }
    Ok(tapped.frames)
}

fn captures(folder: &Path) -> Vec<PathBuf> {
    let mut found = Vec::new();
    for entry in std::fs::read_dir(folder).into_iter().flatten().flatten() {
        let path = entry.path();
        if path.is_dir() {
            found.extend(captures(&path));
        } else if path.extension().is_some_and(|extension| extension == "d3bcap") {
            found.push(path);
        }
    }
    found.sort();
    found
}

#[tokio::test(start_paused = true)]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
async fn the_owners_captures_go_through_the_relay() {
    let folder = PathBuf::from(std::env::var_os("D3B_CAPTURES").unwrap_or_else(|| panic!("set D3B_CAPTURES")));
    let mut failures = Vec::new();
    let (mut phases, mut over, mut games, mut tapped) = (0, 0, 0, 0);
    for path in captures(&folder) {
        let name = path.strip_prefix(&folder).unwrap_or(&path).display().to_string();
        let capture = match std::fs::read(&path)
            .map_err(|error| error.to_string())
            .and_then(|bytes| read(&bytes))
        {
            Ok(capture) => capture,
            Err(error) => {
                failures.push(format!("{name}: {error}"));
                continue;
            }
        };
        // A game connection by its content, whatever port the C# relay listened on: a ticket in its first frame.
        let result = if let Some(ticket) = first_ticket(&joined(&capture.records, true)) {
            games += 1;
            game(&name, &capture, ticket).await.map(|frames| tapped += frames)
        } else {
            phases += 1;
            connection_phase(&name, &capture)
                .await
                .map(|handed| over += usize::from(handed == Handed::Over))
        };
        if let Err(failure) = result {
            failures.push(failure);
        }
    }
    eprintln!(
        "{phases} connection phases ({over} handed over), {games} game connections ({tapped} frames tapped), {} failures",
        failures.len()
    );
    assert!(phases + games > 0, "no capture in {}", folder.display());
    assert!(failures.is_empty(), "{}", failures.join("\n"));
}

/// A capture of `format` holding `records`, after a header of our own.
fn capture(format: i32, records: &[u8]) -> Vec<u8> {
    let mut bytes = b"D3B-CAPT".to_vec();
    bytes.extend_from_slice(&format.to_le_bytes());
    bytes.extend_from_slice(&0_i64.to_le_bytes());
    for text in ["3.7.13.17", "127.0.13.59:5556", "game.example.com", "203.0.113.7:5555"] {
        bytes.push(u8::try_from(text.len()).unwrap_or_default());
        bytes.extend_from_slice(text.as_bytes());
    }
    bytes.extend_from_slice(records);
    bytes
}

#[test]
fn reads_a_capture_and_stops_at_a_record_cut_short() {
    // A record of 128 bytes, its offset and its length in two bytes each.
    let mut long = vec![2, 0x80, 0x01, 0x80, 0x01];
    long.extend_from_slice(&[7; 128]);
    let mut records = vec![1, 0, 3, b'a', b'b', b'c', 4, 7, 5, 9, 2, b'x', b'y'];
    records.extend_from_slice(&long);
    records.extend_from_slice(&[2, 9, 5, b'x']);

    let read = read(&capture(2, &records)).unwrap_or_else(|error| panic!("{error}"));

    assert!(
        read.records
            == [
                Record::FromClient(b"abc".to_vec()),
                Record::Other,
                Record::Other,
                Record::FromServer(vec![7; 128]),
            ]
    );
}

#[test]
fn refuses_what_is_not_a_capture_it_reads() {
    assert!(read(b"NOT-CAPT").is_err());
    assert!(read(&capture(0, &[])).is_err());
    assert!(read(&capture(3, &[])).is_err());
    assert!(read(&capture(2, &[6, 0])).is_err());
    let mut huge = vec![1, 0];
    wire::write_varint(RECORD_LIMIT + 1, &mut huge);
    assert!(read(&capture(2, &huge)).is_err());
    let mut overflowing = vec![1, 0];
    wire::write_varint(u64::MAX, &mut overflowing);
    assert!(read(&capture(1, &overflowing)).is_err());
}
