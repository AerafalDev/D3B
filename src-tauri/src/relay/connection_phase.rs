//! The connection phase through the relay (`docs/relay.md`, "The connections"; inferred, `main:docs/client.md`, "The
//! connection phase on the wire", 3.7.12.12 and 3.7.13.17): the client's identification and server selection, and
//! their answers. Every byte passes untouched but the server-selection answer, whose game host and ports become the
//! relay's, so that the game connection comes through it too.

use std::net::SocketAddr;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;

use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use tokio::task::JoinSet;
use tokio::time::Instant;

use super::ankama_config::is_host_name;
use super::handover::{GameServer, Handover, Ticket, Waiting};
use super::wire::{self, Field, Value};
use super::{ADDRESS, GAME_PORT, frame};

/// How long a server frame may take to come whole once begun: the selection's answer came 29 ms after the request
/// (inferred, 3.7.12.12).
const HOLD: Duration = Duration::from_secs(5);
/// The most of one server frame that is held; the answers are 455 and 83 bytes (inferred, 3.7.12.12).
const HOLD_LIMIT: usize = 64 * 1024;

/// The longest build read from an identification; the client's is 9 bytes (verified, 3.7.13.17).
const BUILD_LIMIT: usize = 32;

/// What a connection phase did, for the relay's status.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Handed {
    /// The selection's answer was rewritten: the game connection comes to the relay.
    Over,
    /// The client asked for a server and the answer was not understood: it passed untouched, so the game connection
    /// goes straight to Ankama.
    NotUnderstood,
    /// The client never chose a server.
    NotAsked,
}

/// Accepts connection phases on `listener`, each relayed to Ankama's connection host `host` (as the last configuration
/// names it) on the port the client chose, until the task is aborted; its connections end with it. `report` hears how
/// each one ended.
pub(crate) async fn serve(
    listener: TcpListener,
    host: Arc<Mutex<String>>,
    handover: Arc<Mutex<Handover>>,
    report: impl Fn(Handed) + Clone + Send + 'static,
) {
    let port = listener.local_addr().map_or(0, |address| address.port());
    let mut connections = JoinSet::new();
    loop {
        let Ok((client, _)) = listener.accept().await else {
            // Out of sockets, say: wait rather than spin.
            tokio::time::sleep(Duration::from_millis(100)).await;
            continue;
        };
        let host = host.lock().unwrap_or_else(std::sync::PoisonError::into_inner).clone();
        let handover = Arc::clone(&handover);
        let report = report.clone();
        connections.spawn(async move {
            let Ok(server) = connect(&host, port).await else {
                return;
            };
            let _ = client.set_nodelay(true);
            let handed = relay(client, server, &handover).await;
            report(handed);
        });
        while connections.try_join_next().is_some() {}
    }
}

/// Connects to `host` on `port`, trying each of its addresses: Ankama's hosts are load balancers of three addresses,
/// resolved at each connection (inferred, `main:docs/client.md`, "Configuration"). A loopback address is skipped, so
/// the relay never connects to itself.
pub(crate) async fn connect(host: &str, port: u16) -> std::io::Result<TcpStream> {
    /// How long a connection to one of Ankama's addresses may take.
    const CONNECT: Duration = Duration::from_secs(10);

    let mut last = std::io::Error::new(std::io::ErrorKind::NotFound, "the host has no address");
    let addresses: Vec<SocketAddr> = tokio::net::lookup_host((host, port)).await?.collect();
    for address in addresses.into_iter().filter(|address| !is_loopback(address)) {
        match tokio::time::timeout(CONNECT, TcpStream::connect(address)).await {
            Ok(Ok(stream)) => {
                let _ = stream.set_nodelay(true);
                return Ok(stream);
            }
            Ok(Err(error)) => last = error,
            Err(_) => last = std::io::Error::from(std::io::ErrorKind::TimedOut),
        }
    }
    Err(last)
}

/// Whether `address` reaches this machine, an IPv4 address written as IPv6 included.
fn is_loopback(address: &SocketAddr) -> bool {
    address.ip().to_canonical().is_loopback()
}

/// Relays one connection phase both ways until both sides are done, or a length that is not a frame's closes it. The
/// build the client's identification names goes to the handover with the ticket.
pub(crate) async fn relay<C, S>(client: C, server: S, handover: &Mutex<Handover>) -> Handed
where
    C: AsyncRead + AsyncWrite + Unpin,
    S: AsyncRead + AsyncWrite + Unpin,
{
    let (mut client_read, mut client_write) = tokio::io::split(client);
    let (mut server_read, mut server_write) = tokio::io::split(server);
    let asked = AtomicBool::new(false);
    let build = OnceLock::new();
    let upstream = async {
        let closing = watch_requests(&mut client_read, &mut server_write, &asked, &build).await;
        let _ = server_write.shutdown().await;
        closing
    };
    let downstream = async {
        let ended = pass_answers(&mut server_read, &mut client_write, &asked, &build, handover).await;
        let _ = client_write.shutdown().await;
        ended
    };
    tokio::pin!(upstream, downstream);
    // Both directions run in this one task, so the client's request is noted before the server's answer is read. A
    // length that is not a frame's closes both at once (`docs/relay.md`, "The connections").
    let handed = tokio::select! {
        closing = &mut upstream => {
            if closing { Handed::NotAsked } else { (&mut downstream).await.0 }
        }
        (handed, closing) = &mut downstream => {
            if !closing {
                (&mut upstream).await;
            }
            handed
        }
    };
    if handed == Handed::NotAsked && asked.load(Ordering::Relaxed) {
        return Handed::NotUnderstood;
    }
    handed
}

/// Copies the client's bytes to the server as they come, noting the first build an identification names and when a
/// frame asks for a server. Frames are only read, never changed; past one too large to hold, the rest is only copied.
/// `true` when a length is not a frame's, which closes the connection.
async fn watch_requests(
    from: &mut (impl AsyncRead + Unpin),
    to: &mut (impl AsyncWrite + Unpin),
    asked: &AtomicBool,
    build: &OnceLock<String>,
) -> bool {
    let mut buffer = Vec::new();
    let mut framed = true;
    // On the heap, so that each connection's task stays small.
    let mut chunk = vec![0; 8192];
    loop {
        let read = match from.read(&mut chunk).await {
            Ok(0) | Err(_) => return false,
            Ok(read) => read,
        };
        if !framed {
            if to.write_all(&chunk[..read]).await.is_err() {
                return false;
            }
            continue;
        }
        buffer.extend_from_slice(&chunk[..read]);
        loop {
            match frame::header(&buffer) {
                Ok(Some((header, body))) if buffer.len() >= header + body => {
                    let frame = &buffer[header..header + body];
                    if build.get().is_none()
                        && let Some(version) = client_version(frame)
                    {
                        let _ = build.set(version);
                    }
                    if is_selection_request(frame) {
                        asked.store(true, Ordering::Relaxed);
                    }
                    buffer.drain(..header + body);
                }
                Ok(Some((_, body))) if body > HOLD_LIMIT => {
                    framed = false;
                    buffer = Vec::new();
                    break;
                }
                Ok(_) => break,
                Err(_) => return true,
            }
        }
        // Written once read, so that a length that is not a frame's never reaches the server.
        if to.write_all(&chunk[..read]).await.is_err() {
            return false;
        }
    }
}

/// Copies the server's bytes to the client a whole frame at a time, so that the selection's answer, once the client
/// asked for a server, is rewritten when understood, its ticket handed over with the client's `build`. A frame not
/// whole within [`HOLD`] of its first byte, or over [`HOLD_LIMIT`], passes as it came, and the rest is only copied.
/// Also `true` when a length is not a frame's, which closes the connection.
async fn pass_answers(
    from: &mut (impl AsyncRead + Unpin),
    to: &mut (impl AsyncWrite + Unpin),
    asked: &AtomicBool,
    build: &OnceLock<String>,
    handover: &Mutex<Handover>,
) -> (Handed, bool) {
    let mut buffer: Vec<u8> = Vec::new();
    // On the heap, so that each connection's task stays small.
    let mut chunk = vec![0; 8192];
    let mut handed = Handed::NotAsked;
    let mut begun: Option<Instant> = None;
    loop {
        let read = match begun {
            Some(begun) => tokio::time::timeout_at(begun + HOLD, from.read(&mut chunk)).await,
            None => Ok(from.read(&mut chunk).await),
        };
        let read = match read {
            Ok(Ok(0) | Err(_)) => {
                let _ = to.write_all(&buffer).await;
                return (handed, false);
            }
            Ok(Ok(read)) => read,
            Err(_) => return (copy(&buffer, from, to, handed).await, false),
        };
        buffer.extend_from_slice(&chunk[..read]);
        let mut completed = false;
        loop {
            match frame::header(&buffer) {
                Ok(Some((header, body))) if buffer.len() >= header + body => {
                    let total = header + body;
                    let rewritten = (handed == Handed::NotAsked && asked.load(Ordering::Relaxed))
                        .then(|| {
                            let now = Instant::now().into_std();
                            rewrite_selection(&buffer[header..total], handover, build.get().cloned(), now)
                        })
                        .flatten();
                    if rewritten.is_some() {
                        // Handed over even if the client is gone: the ticket waits in the handover.
                        handed = Handed::Over;
                    }
                    let written = to.write_all(rewritten.as_deref().unwrap_or(&buffer[..total])).await;
                    if written.is_err() {
                        return (handed, false);
                    }
                    buffer.drain(..total);
                    completed = true;
                    if rewritten.is_some() {
                        return (copy(&buffer, from, to, handed).await, false);
                    }
                }
                Ok(Some((_, body))) if body > HOLD_LIMIT => return (copy(&buffer, from, to, handed).await, false),
                Ok(_) => break,
                Err(_) => return (handed, true),
            }
        }
        // The hold runs from the first byte of the frame held, never through a silence between frames.
        begun = match (buffer.is_empty(), completed, begun) {
            (true, _, _) => None,
            (false, false, Some(begun)) => Some(begun),
            (false, _, _) => Some(Instant::now()),
        };
    }
}

/// Writes what is held, then copies the rest untouched.
async fn copy(
    held: &[u8],
    from: &mut (impl AsyncRead + Unpin),
    to: &mut (impl AsyncWrite + Unpin),
    handed: Handed,
) -> Handed {
    if to.write_all(held).await.is_ok() {
        let _ = tokio::io::copy(from, to).await;
    }
    handed
}

/// Whether a client frame's body asks for a server: `request = 1 { id = 1, server_selection = 4 }` (path 1.4,
/// `artifacts/studies/relay.md`, addendum).
fn is_selection_request(body: &[u8]) -> bool {
    only(body, 1).is_some_and(|request| {
        wire::fields(request)
            .map_while(Result::ok)
            .any(|field| field.number == 4)
    })
}

/// The client's build its identification names: `request = 1 { identification = 3 { client_version = 5 } }` (path
/// 1.3.5, verified: equal to the build each of the owner's 171 connection phases of 3.7.12.12 and 3.7.13.17 recorded,
/// `artifacts/studies/protocol.md`, section 6). `None` unless read whole and made of digits and dots, at most
/// [`BUILD_LIMIT`] bytes, as the `version` file writes it: it names capture files and picks the protocol's pins.
fn client_version(body: &[u8]) -> Option<String> {
    let request = whole(only(body, 1)?)?;
    let identification = whole(bytes_of(&request, 3)?)?;
    let version = std::str::from_utf8(bytes_of(&identification, 5)?).ok()?;
    let digits = |part: &str| !part.is_empty() && part.bytes().all(|byte| byte.is_ascii_digit());
    (version.len() <= BUILD_LIMIT && version.split('.').all(digits)).then(|| version.to_owned())
}

/// The selection's answer rewritten to send the game connection to the relay, as a whole frame; its ticket and real
/// game server are put in the handover with the client's `build`. `None` when `body` is not that answer, read whole and
/// of the expected shape: `response = 2 { id = 1, server_selection = 4 { success = 1 { ticket = 1, host = 2, ports = 3
/// } } }`, a ticket of 32 letters and digits, a host name, packed ports from 1 to 65535 (path 2.4.1,
/// `artifacts/studies/relay.md`, addendum; the shape checks are the owner's choice for builds not measured,
/// 2026-11-11).
fn rewrite_selection(
    body: &[u8],
    handover: &Mutex<Handover>,
    build: Option<String>,
    now: std::time::Instant,
) -> Option<Vec<u8>> {
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
    let ticket_waiting = Ticket::new(ticket)?;
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
    handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).put(
        ticket_waiting,
        Waiting { server, build },
        now,
    );
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
    use tokio::io::{AsyncReadExt, AsyncWriteExt, DuplexStream};

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

    fn framed(body: &[u8]) -> Vec<u8> {
        let mut out = Vec::new();
        frame::encode(body, &mut out);
        out
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

    /// The client's identification, its version at path 1.3.5 as on 3.7.13.17.
    fn identification_of(version: &[u8]) -> Vec<u8> {
        let identification = message(&[bytes(1, b"device"), bytes(3, b"fr"), bytes(5, version)]);
        let request = message(&[bytes(1, b"1"), bytes(3, &identification)]);
        message(&[bytes(1, &request)])
    }

    fn identification() -> Vec<u8> {
        identification_of(b"3.7.13.17")
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

        let rewritten = rewrite_selection(
            &answer(),
            &handover,
            Some("3.7.13.17".into()),
            std::time::Instant::now(),
        )
        .unwrap_or_else(|| panic!("not rewritten"));

        assert_eq!(
            read_answer(&rewritten),
            (TICKET.to_vec(), "127.0.13.59".to_owned(), vec![5556])
        );
        let first_frame = message(&[bytes(1, TICKET)]);
        let waiting = handover
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .take_from(&first_frame, std::time::Instant::now());
        assert_eq!(
            waiting,
            Some(Waiting {
                server: GameServer {
                    host: "game.example.com".into(),
                    port: 5555
                },
                build: Some("3.7.13.17".into())
            })
        );
    }

    #[test]
    fn leaves_an_answer_of_another_shape() {
        let ports = packed(&[5555]);
        let short = b"0123456789abcdef";
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
            identification(),
            Vec::new(),
        ] {
            let handover = Mutex::new(Handover::default());

            assert_eq!(
                rewrite_selection(&body, &handover, None, std::time::Instant::now()),
                None,
                "{body:?}"
            );
            assert_eq!(
                handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).len(),
                0
            );
        }
    }

    /// A field whose length is written in two bytes where one is enough: read as the same value, but not encoded
    /// back to the same bytes.
    fn padded(number: u32, value: &[u8]) -> Vec<u8> {
        let length = u8::try_from(value.len()).ok().filter(|&length| length < 0x80);
        let length = length.unwrap_or_else(|| panic!("a value of one length byte"));
        let mut out = Vec::new();
        wire::write_varint(u64::from(number) << 3 | 2, &mut out);
        out.extend_from_slice(&[length | 0x80, 0x00]);
        out.extend_from_slice(value);
        out
    }

    #[test]
    fn leaves_an_answer_not_written_minimally_at_any_level() {
        let ports = packed(&[5555, 443]);
        let ticket = bytes(1, TICKET);
        let host = bytes(2, b"game.example.com");
        let ports = bytes(3, &ports);
        let success = message(&[ticket, host, ports]);
        let selection = message(&[bytes(1, &success)]);
        let response = message(&[bytes(1, b"7"), bytes(4, &selection)]);

        let mut padded_success = padded(1, TICKET);
        padded_success.extend_from_slice(&message(&[host, ports]));
        let padded_selection = padded(1, &success);
        let mut padded_response = message(&[bytes(1, b"7")]);
        padded_response.extend_from_slice(&padded(4, &selection));
        for body in [
            message(&[bytes(
                2,
                &message(&[bytes(1, b"7"), bytes(4, &message(&[bytes(1, &padded_success)]))]),
            )]),
            message(&[bytes(2, &message(&[bytes(1, b"7"), bytes(4, &padded_selection)]))]),
            message(&[bytes(2, &padded_response)]),
            padded(2, &response),
        ] {
            let handover = Mutex::new(Handover::default());

            assert_eq!(
                rewrite_selection(&body, &handover, None, std::time::Instant::now()),
                None,
                "{body:?}"
            );
            assert_eq!(
                handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).len(),
                0
            );
        }
        // The same answer written minimally is rewritten: only the padding stops it.
        let handover = Mutex::new(Handover::default());
        assert!(
            rewrite_selection(
                &message(&[bytes(2, &response)]),
                &handover,
                None,
                std::time::Instant::now()
            )
            .is_some()
        );
    }

    #[test]
    fn reads_the_clients_build_from_its_identification() {
        assert_eq!(client_version(&identification()).as_deref(), Some("3.7.13.17"));
        assert_eq!(client_version(&identification_of(b"4")).as_deref(), Some("4"));
    }

    #[test]
    fn reads_no_build_but_digits_and_dots_at_its_path() {
        let longest = "11".to_owned() + &".1".repeat(15);
        let long = longest.clone() + "1";
        let request = message(&[bytes(1, b"1"), bytes(4, &message(&[bytes(5, b"3.7.13.17")]))]);
        let mut padded_request = message(&[bytes(1, b"1")]);
        padded_request.extend_from_slice(&padded(3, &message(&[bytes(5, b"3.7.13.17")])));
        let mut padded_identification = message(&[bytes(1, b"device")]);
        padded_identification.extend_from_slice(&padded(5, b"3.7.13.17"));
        let twice = message(&[bytes(5, b"3.7.13.17"), bytes(5, b"3.7.13.18")]);
        for body in [
            identification_of(b""),
            identification_of(b"../3.7.13.17"),
            identification_of(b"3.7.13.17 "),
            identification_of(b"3..13.17"),
            identification_of(b".3.7"),
            identification_of(b"3.7."),
            identification_of(long.as_bytes()),
            identification_of(&[0xff]),
            message(&[bytes(1, &request)]),
            message(&[bytes(1, &padded_request)]),
            message(&[bytes(1, &message(&[bytes(1, b"1"), bytes(3, &padded_identification)]))]),
            message(&[bytes(1, &message(&[bytes(1, b"1"), bytes(3, &twice)]))]),
            selection_request(),
            answer(),
            Vec::new(),
        ] {
            assert_eq!(client_version(&body), None, "{body:?}");
        }
        assert_eq!(client_version(&identification_of(longest.as_bytes())), Some(longest));
    }

    #[test]
    fn knows_the_selection_request() {
        assert!(is_selection_request(&selection_request()));
        assert!(!is_selection_request(&identification()));
        assert!(!is_selection_request(&answer()));
        assert!(!is_selection_request(&[0xff]));
    }

    /// A connection phase relayed over in-memory streams: the client's end and the server's end.
    fn phase() -> (DuplexStream, DuplexStream, tokio::task::JoinHandle<(Handed, usize)>) {
        let (client, client_side) = tokio::io::duplex(1 << 16);
        let (server, server_side) = tokio::io::duplex(1 << 16);
        let relay = tokio::spawn(async move {
            let handover = Mutex::new(Handover::default());
            let handed = relay(client_side, server_side, &handover).await;
            let waiting = handover.lock().unwrap_or_else(std::sync::PoisonError::into_inner).len();
            (handed, waiting)
        });
        (client, server, relay)
    }

    async fn read_exactly(stream: &mut DuplexStream, length: usize) -> Vec<u8> {
        let mut bytes = vec![0; length];
        stream
            .read_exact(&mut bytes)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        bytes
    }

    async fn send(stream: &mut DuplexStream, bytes: &[u8]) {
        stream.write_all(bytes).await.unwrap_or_else(|error| panic!("{error}"));
    }

    #[tokio::test]
    async fn relays_the_phase_and_rewrites_only_the_answer() {
        let (mut client, mut server, relay) = phase();
        let identification = framed(&identification());
        let identified = framed(&message(&[bytes(2, &message(&[bytes(1, b"1"), bytes(3, b"account")]))]));
        let request = framed(&selection_request());
        let answer = framed(&answer());

        send(&mut client, &identification).await;
        assert_eq!(read_exactly(&mut server, identification.len()).await, identification);
        send(&mut server, &identified).await;
        assert_eq!(read_exactly(&mut client, identified.len()).await, identified);
        send(&mut client, &request).await;
        assert_eq!(read_exactly(&mut server, request.len()).await, request);
        // The answer comes cut in two reads, as the network may cut it.
        send(&mut server, &answer[..10]).await;
        tokio::task::yield_now().await;
        send(&mut server, &answer[10..]).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(read_answer(&received).1, "127.0.13.59");
        assert_eq!(relay.await.ok(), Some((Handed::Over, 1)));
    }

    /// The build handed over with the ticket, once a client chose a server in one read after identifying itself with
    /// `identifications`.
    async fn build_handed_over(identifications: &[Vec<u8>]) -> Option<Option<String>> {
        let (mut client, client_side) = tokio::io::duplex(1 << 16);
        let (mut server, server_side) = tokio::io::duplex(1 << 16);
        let handover = Mutex::new(Handover::default());
        let talk = async move {
            let mut requests: Vec<u8> = identifications.iter().flat_map(|body| framed(body)).collect();
            requests.extend_from_slice(&framed(&selection_request()));
            send(&mut client, &requests).await;
            read_exactly(&mut server, requests.len()).await;
            send(&mut server, &framed(&answer())).await;
            server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
            let _ = client.read_to_end(&mut Vec::new()).await;
        };
        let (handed, ()) = tokio::join!(relay(client_side, server_side, &handover), talk);
        assert_eq!(handed, Handed::Over);
        let first_frame = message(&[bytes(1, TICKET)]);
        handover
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .take_from(&first_frame, std::time::Instant::now())
            .map(|waiting| waiting.build)
    }

    #[tokio::test]
    async fn hands_the_clients_build_over_with_its_ticket() {
        assert_eq!(
            build_handed_over(&[identification()]).await,
            Some(Some("3.7.13.17".into()))
        );
    }

    #[tokio::test]
    async fn hands_over_no_build_when_the_client_named_none() {
        assert_eq!(build_handed_over(&[]).await, Some(None));
    }

    #[tokio::test]
    async fn hands_over_the_first_build_named() {
        let identifications = [identification_of(b"3.7.13.17"), identification_of(b"3.7.14.1")];

        assert_eq!(
            build_handed_over(&identifications).await,
            Some(Some("3.7.13.17".into()))
        );
    }

    #[tokio::test]
    async fn an_answer_before_any_selection_passes_untouched() {
        let (mut client, mut server, relay) = phase();
        let answer = framed(&answer());

        send(&mut server, &answer).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(received, answer);
        assert_eq!(relay.await.ok(), Some((Handed::NotAsked, 0)));
    }

    #[tokio::test]
    async fn an_answer_not_understood_passes_untouched() {
        let (mut client, mut server, relay) = phase();
        let request = framed(&selection_request());
        let error = framed(&message(&[bytes(
            2,
            &message(&[bytes(1, b"3"), bytes(4, &message(&[bytes(2, b"\x02")]))]),
        )]));

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &error).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(received, error);
        assert_eq!(relay.await.ok(), Some((Handed::NotUnderstood, 0)));
    }

    #[test]
    fn never_connects_back_to_this_machine() {
        for address in ["127.0.13.59:5555", "[::1]:5555", "[::ffff:127.0.13.59]:5555"] {
            assert!(
                is_loopback(&address.parse().unwrap_or_else(|error| panic!("{error}"))),
                "{address}"
            );
        }
        assert!(!is_loopback(
            &"203.0.113.7:5555".parse().unwrap_or_else(|error| panic!("{error}"))
        ));
    }

    #[tokio::test(start_paused = true)]
    async fn a_silence_before_the_selection_holds_nothing() {
        let (mut client, mut server, relay) = phase();
        let identified = framed(&message(&[bytes(2, &message(&[bytes(1, b"1"), bytes(3, b"account")]))]));
        let request = framed(&selection_request());
        let answer = framed(&answer());

        send(&mut server, &identified).await;
        assert_eq!(read_exactly(&mut client, identified.len()).await, identified);
        // The player takes two minutes to choose a server.
        tokio::time::sleep(Duration::from_secs(120)).await;
        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &answer).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(read_answer(&received).1, "127.0.13.59");
        assert_eq!(relay.await.ok(), Some((Handed::Over, 1)));
    }

    #[tokio::test(start_paused = true)]
    async fn the_hold_starts_again_with_each_frame() {
        let (mut client, mut server, relay) = phase();
        let identified = framed(&message(&[bytes(2, &message(&[bytes(1, b"1"), bytes(3, b"account")]))]));
        let request = framed(&selection_request());
        let answer = framed(&answer());
        let mut both = identified.clone();
        both.extend_from_slice(&answer[..10]);

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &identified[..5]).await;
        tokio::time::sleep(Duration::from_millis(4_900)).await;
        send(&mut server, &both[5..]).await;
        tokio::time::sleep(Duration::from_millis(600)).await;
        send(&mut server, &answer[10..]).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(&received[..identified.len()], identified);
        assert_eq!(read_answer(&received[identified.len()..]).1, "127.0.13.59");
        assert_eq!(relay.await.ok().map(|(handed, _)| handed), Some(Handed::Over));
    }

    #[tokio::test]
    async fn bytes_after_the_answer_pass_too() {
        let (mut client, mut server, relay) = phase();
        let request = framed(&selection_request());
        let mut answer_and_more = framed(&answer());
        answer_and_more.extend_from_slice(b"\x03abc");

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &answer_and_more).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        let mut expected =
            rewrite_selection(&answer(), &Mutex::default(), None, std::time::Instant::now()).unwrap_or_default();
        expected.extend_from_slice(b"\x03abc");
        assert_eq!(received, expected);
        assert_eq!(relay.await.ok().map(|(handed, _)| handed), Some(Handed::Over));
    }

    #[tokio::test]
    async fn a_frame_too_large_to_hold_passes_at_once() {
        let (mut client, mut server, relay) = phase();
        let request = framed(&selection_request());
        let mut header = Vec::new();
        wire::write_varint(HOLD_LIMIT as u64 + 1, &mut header);

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &header).await;
        send(&mut server, b"start").await;

        // Passed as it came, without waiting for the rest.
        let early = tokio::time::timeout(Duration::from_millis(100), read_exactly(&mut client, header.len() + 5)).await;
        assert!(early.is_ok(), "held");
        drop(server);
        drop(client);
        assert_eq!(relay.await.ok().map(|(handed, _)| handed), Some(Handed::NotUnderstood));
    }

    /// Whether both ends see the relay close, within a second of paused time, though neither closed its side.
    async fn closed_both_ways(client: &mut DuplexStream, server: &mut DuplexStream) -> bool {
        let mut rest = Vec::new();
        let client_closed = tokio::time::timeout(Duration::from_secs(1), client.read_to_end(&mut rest)).await;
        let server_closed = tokio::time::timeout(Duration::from_secs(1), server.read_to_end(&mut rest)).await;
        matches!(client_closed, Ok(Ok(_))) && matches!(server_closed, Ok(Ok(_))) && rest.is_empty()
    }

    #[tokio::test(start_paused = true)]
    async fn a_server_length_that_is_not_a_frames_closes_the_connection() {
        let (mut client, mut server, relay) = phase();

        send(&mut server, &[0x80; 5]).await;

        assert!(closed_both_ways(&mut client, &mut server).await);
        assert!(relay.await.is_ok());
    }

    #[tokio::test(start_paused = true)]
    async fn a_client_length_that_is_not_a_frames_closes_the_connection() {
        let (mut client, mut server, relay) = phase();

        send(&mut client, &[0x80; 5]).await;

        assert!(closed_both_ways(&mut client, &mut server).await);
        assert!(relay.await.is_ok());
    }

    #[tokio::test(start_paused = true)]
    async fn a_frame_not_whole_in_time_passes_as_it_came() {
        let (mut client, mut server, relay) = phase();
        let request = framed(&selection_request());
        let answer = framed(&answer());

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &answer[..10]).await;
        // Five seconds, as docs/relay.md says, written out so that the test pins it: nothing before, all after.
        let early = tokio::time::timeout(Duration::from_millis(4_999), read_exactly(&mut client, 10)).await;
        assert!(early.is_err(), "passed before five seconds");
        tokio::time::sleep(Duration::from_millis(2)).await;

        // Already there once the hold is over: the paused clock would otherwise move on to whenever they come.
        let held = tokio::time::timeout(Duration::from_millis(1), read_exactly(&mut client, 10)).await;
        assert_eq!(held.ok().as_deref(), Some(&answer[..10]));
        send(&mut server, &answer[10..]).await;
        assert_eq!(read_exactly(&mut client, answer.len() - 10).await, &answer[10..]);
        drop(server);
        drop(client);
        assert_eq!(relay.await.ok().map(|(handed, _)| handed), Some(Handed::NotUnderstood));
    }

    #[tokio::test]
    async fn a_frame_that_is_not_a_message_passes_untouched() {
        let (mut client, mut server, relay) = phase();
        let request = framed(&selection_request());
        let garbage = framed(&[0xff; 12]);

        send(&mut client, &request).await;
        read_exactly(&mut server, request.len()).await;
        send(&mut server, &garbage).await;
        server.shutdown().await.unwrap_or_else(|error| panic!("{error}"));
        let mut received = Vec::new();
        client
            .read_to_end(&mut received)
            .await
            .unwrap_or_else(|error| panic!("{error}"));
        drop(client);

        assert_eq!(received, garbage);
        assert_eq!(relay.await.ok(), Some((Handed::NotUnderstood, 0)));
    }
}
