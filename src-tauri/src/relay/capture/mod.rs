//! Captures of game connections (`docs/protocol.md`, "Captures"), in the C# version's `.d3bcap` format 2 (inferred,
//! `main:src/D3B.Network/Captures/CaptureLayout.cs`): the magic `D3B-CAPT`, the format as a little-endian `i32`, the
//! start in Unix milliseconds as a little-endian `i64`, four strings after their 7-bit lengths (the client's build,
//! the listener, the server's host, the server), then records of a kind byte, the microseconds since the start as a
//! 7-bit number, and for bytes a 7-bit length and the bytes as one read gave them. Nothing follows the last record, so
//! a capture cut short reads up to its last whole record. A capture holds the account's ticket: it is never shown.

mod writer;

pub(crate) use writer::{Recorder, Recording};

const MAGIC: &[u8; 8] = b"D3B-CAPT";
const FORMAT: i32 = 2;
/// A record of bytes the client sent.
const FROM_CLIENT: u8 = 1;
/// A record of bytes the server sent.
const FROM_SERVER: u8 = 2;

#[cfg(test)]
pub(crate) use reader::{Capture, Record, read};

#[cfg(test)]
mod reader {
    use super::super::wire;

    /// The most one record holds, as the C# reader bounds it: only a damaged file reaches it.
    pub(crate) const RECORD_LIMIT: u64 = 64 * 1024 * 1024;

    /// One capture: the client's build its header names, and its records in order.
    pub(crate) struct Capture {
        pub(crate) build: String,
        pub(crate) records: Vec<Record>,
        /// When each record came, in microseconds since the capture's start.
        pub(crate) micros: Vec<u64>,
    }

    #[derive(Debug, PartialEq, Eq)]
    pub(crate) enum Record {
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

    /// Reads a capture of format 1 or 2; a record cut short ends it, as a crash leaves it.
    pub(crate) fn read(bytes: &[u8]) -> Result<Capture, String> {
        let rest = bytes.strip_prefix(super::MAGIC).ok_or("not a D3B capture")?;
        let (format, rest) = rest.split_first_chunk::<4>().ok_or("cut in its header")?;
        let format = i32::from_le_bytes(*format);
        if !matches!(format, 1 | 2) {
            return Err(format!("format {format}"));
        }
        let (_, mut rest) = rest.split_first_chunk::<8>().ok_or("cut in its header")?;
        let mut texts = Vec::new();
        for _ in 0..4 {
            let (length, used) = length(rest)?.ok_or("cut in its header")?;
            let end = used
                .checked_add(length)
                .filter(|&end| end <= rest.len())
                .ok_or("cut in its header")?;
            texts.push(String::from_utf8_lossy(&rest[used..end]).into_owned());
            rest = &rest[end..];
        }
        let (mut records, mut micros) = (Vec::new(), Vec::new());
        while let Some((&kind, tail)) = rest.split_first() {
            let Some((at, used)) = wire::read_varint(tail) else {
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
                        super::FROM_CLIENT => Record::FromClient(data.to_vec()),
                        super::FROM_SERVER => Record::FromServer(data.to_vec()),
                        _ => Record::Other,
                    }
                }
                3 | 4 => Record::Other,
                other => return Err(format!("a record of kind {other}")),
            };
            records.push(record);
            micros.push(at);
            rest = tail;
        }
        Ok(Capture {
            build: texts.swap_remove(0),
            records,
            micros,
        })
    }

    #[cfg(test)]
    mod tests {
        use super::*;

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

            assert_eq!(read.build, "3.7.13.17");
            assert_eq!(
                read.records,
                [
                    Record::FromClient(b"abc".to_vec()),
                    Record::Other,
                    Record::Other,
                    Record::FromServer(vec![7; 128]),
                ]
            );
            // Each record's time, the first number after its kind.
            assert_eq!(read.micros, [0, 7, 9, 128]);
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
    }
}
