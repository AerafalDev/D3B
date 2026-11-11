//! The owner's captures replayed through the relay (`docs/relay.md`, "Tests"). They are the C# version's `.d3bcap`
//! files, format 1 or 2 (inferred, `main:src/D3B.Network/Captures/CaptureLayout.cs`), which record each side's bytes as
//! one read gave them, before any rewrite. They hold the account's login token and tickets: a failure names the file, a
//! frame's index and lengths, never a byte of content. Ignored unless run with `D3B_CAPTURES` naming their folder.

use std::path::{Path, PathBuf};
use std::sync::Mutex;
use std::time::Instant;

use tokio::io::{AsyncReadExt, AsyncWriteExt, DuplexStream};

use super::connection_phase::{self, Handed};
use super::handover::{GameServer, Handover, Ticket};
use super::wire::{self, Value};
use super::{GAME_PORT, frame, game_connection};

/// One capture: its listener's port and its records in order.
struct Capture {
    port: u16,
    records: Vec<Record>,
}

#[derive(PartialEq, Eq)]
enum Record {
    FromClient(Vec<u8>),
    FromServer(Vec<u8>),
    /// A side closed, or a frame the C# bot sent itself: nothing to replay.
    Other,
}

/// Reads a capture: the magic `D3B-CAPT`, the format as a little-endian `i32`, the start as a little-endian `i64`,
/// four strings after their 7-bit lengths (client version, listener, server host, server), then records of a kind
/// byte, the microseconds since the start as a 7-bit number, and for bytes a 7-bit length and the bytes. A record cut
/// short ends the capture, as a crash leaves it.
fn read(bytes: &[u8]) -> Result<Capture, String> {
    let rest = bytes.strip_prefix(b"D3B-CAPT").ok_or("not a D3B capture")?;
    let (format, rest) = rest.split_first_chunk::<4>().ok_or("cut in its header")?;
    if !matches!(i32::from_le_bytes(*format), 1 | 2) {
        return Err(format!("format {}", i32::from_le_bytes(*format)));
    }
    let (_, mut rest) = rest.split_first_chunk::<8>().ok_or("cut in its header")?;
    let mut strings = Vec::new();
    for _ in 0..4 {
        let (length, used) = wire::read_varint(rest).ok_or("cut in its header")?;
        let length = usize::try_from(length).map_err(|_| "a header string too long")?;
        let text = rest.get(used..used + length).ok_or("cut in its header")?;
        strings.push(String::from_utf8_lossy(text).into_owned());
        rest = &rest[used + length..];
    }
    let port = strings[1]
        .rsplit(':')
        .next()
        .and_then(|port| port.parse().ok())
        .unwrap_or(0);
    let mut records = Vec::new();
    while let Some((&kind, tail)) = rest.split_first() {
        let Some((_, used)) = wire::read_varint(tail) else {
            break;
        };
        let mut tail = &tail[used..];
        let record = match kind {
            1 | 2 | 5 => {
                let Some((length, used)) = wire::read_varint(tail) else {
                    break;
                };
                let Some(data) = usize::try_from(length)
                    .ok()
                    .and_then(|length| tail.get(used..used + length))
                else {
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
    Ok(Capture { port, records })
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

/// Plays `records` on the client's and the server's ends, each client read waited for at the server's end, so that
/// the relay sees them in their recorded order; then closes both and returns what each end received.
async fn play(records: &[Record], mut client: DuplexStream, mut server: DuplexStream) -> (Vec<u8>, Vec<u8>) {
    let (client_read, mut client_write) = tokio::io::split(&mut client);
    let (server_read, mut server_write) = tokio::io::split(&mut server);
    let mut client_read = client_read;
    let mut server_read = server_read;
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
                    if server_read.read_exact(&mut forwarded).await.is_err() {
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
/// untouched but for one frame, the selection's answer, rewritten when the client asked for a server.
async fn connection_phase(name: &str, capture: &Capture) -> Result<Handed, String> {
    let (client, client_side) = tokio::io::duplex(1 << 20);
    let (server, server_side) = tokio::io::duplex(1 << 20);
    let handover = Mutex::new(Handover::default());
    let relay = connection_phase::relay(client_side, server_side, &handover);
    let (handed, (at_client, at_server)) = tokio::join!(relay, play(&capture.records, client, server));
    if at_server != joined(&capture.records, true) {
        return Err(format!(
            "{name}: the server got {} bytes of the client's {}",
            at_server.len(),
            joined(&capture.records, true).len()
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
    match (handed, changed.as_slice()) {
        (Handed::Over, [index]) => {
            let (header, _) = frame::header(got_frames[*index]).ok().flatten().unwrap_or_default();
            let fields: Vec<_> = wire::fields(&got_frames[*index][header..])
                .map_while(Result::ok)
                .collect();
            if fields.len() != 1 || handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).len() != 1 {
                return Err(format!("{name}: frame {index} was changed into something else"));
            }
            Ok(handed)
        }
        (Handed::NotAsked, []) => Ok(handed),
        // The captures are of builds whose answer the relay reads (3.7.12.12 and 3.7.13.17): one not understood is a
        // bug, not a new build.
        (Handed::NotUnderstood, []) => Err(format!(
            "{name}: the client chose a server and its answer was not understood"
        )),
        (handed, changed) => Err(format!("{name}: {handed:?} with frames {changed:?} changed")),
    }
}

/// The ticket of a game connection's first frame, at path 4.2.2.1 (inferred, `main:docs/client-game.md`, 3.7.13.17).
fn first_ticket(bytes: &[u8]) -> Option<Ticket> {
    let (header, body) = frame::header(bytes).ok().flatten()?;
    let mut message = bytes.get(header..header + body)?;
    for number in [4, 2, 2, 1] {
        message = wire::fields(message)
            .map_while(Result::ok)
            .find_map(|field| match field.value {
                Value::Bytes(bytes) if field.number == number => Some(bytes),
                _ => None,
            })?;
    }
    Ticket::new(message)
}

/// Replays a game connection, its ticket seeded into the handover: every byte reaches the other end untouched.
async fn game(name: &str, capture: &Capture) -> Result<(), String> {
    let from_client = joined(&capture.records, true);
    let ticket = first_ticket(&from_client).ok_or_else(|| format!("{name}: no ticket in the first frame"))?;
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
    let relay = game_connection::relay(client_side, &handover, |_| async move { Some(server_side) }, || {});
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
    Ok(())
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

#[tokio::test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
async fn the_owners_captures_go_through_the_relay() {
    let folder = PathBuf::from(std::env::var_os("D3B_CAPTURES").unwrap_or_else(|| panic!("set D3B_CAPTURES")));
    let mut failures = Vec::new();
    let (mut phases, mut over, mut games) = (0, 0, 0);
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
        let result = if capture.port == GAME_PORT {
            games += 1;
            game(&name, &capture).await
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
        "{phases} connection phases ({over} handed over), {games} game connections, {} failures",
        failures.len()
    );
    assert!(phases + games > 0, "no capture in {}", folder.display());
    assert!(failures.is_empty(), "{}", failures.join("\n"));
}

#[test]
fn reads_a_capture_and_stops_at_a_record_cut_short() {
    let mut bytes = b"D3B-CAPT".to_vec();
    bytes.extend_from_slice(&2_i32.to_le_bytes());
    bytes.extend_from_slice(&0_i64.to_le_bytes());
    for text in ["3.7.13.17", "127.0.13.59:5556", "game.example.com", "203.0.113.7:5555"] {
        bytes.push(u8::try_from(text.len()).unwrap_or_default());
        bytes.extend_from_slice(text.as_bytes());
    }
    bytes.extend_from_slice(&[1, 0, 3, b'a', b'b', b'c', 4, 7, 2, 9, 5, b'x']);

    let capture = read(&bytes).unwrap_or_else(|error| panic!("{error}"));

    assert_eq!(capture.port, 5556);
    assert!(capture.records == [Record::FromClient(b"abc".to_vec()), Record::Other]);
    assert!(read(b"NOT-CAPT").is_err());
}
