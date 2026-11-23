use super::RelayError;
use super::wire;

/// The most a frame may hold. The largest frame seen from a game server is 88,581 bytes (verified, the owner's 54 game
/// captures of 3.7.13.17, `artifacts/studies/protocol.md`); the C# reader bounded frames at 16 MiB too.
pub(crate) const LIMIT: usize = 16 * 1024 * 1024;

/// Splits one direction of a connection into frames as its reads come (`docs/protocol.md`, "The tap"): a frame may
/// come over several reads, 4.6% of the game server's reads ending inside one, and a read may hold several (verified,
/// the owner's game captures of 3.7.13.17). Only the start of a frame not yet whole is kept.
#[derive(Debug, Default)]
pub(crate) struct Frames {
    pending: Vec<u8>,
}

impl Frames {
    /// Takes the bytes of one read and gives `each` the body of every frame they complete, in order. After an error
    /// the stream is no longer in frames: the reader must be dropped.
    pub(crate) fn push(&mut self, bytes: &[u8], mut each: impl FnMut(&[u8])) -> Result<(), RelayError> {
        if self.pending.is_empty() {
            // Most reads end on a frame: split them where they lie, with no copy.
            let used = split(bytes, &mut each)?;
            self.pending.extend_from_slice(&bytes[used..]);
        } else {
            self.pending.extend_from_slice(bytes);
            let used = split(&self.pending, &mut each)?;
            self.pending.drain(..used);
        }
        Ok(())
    }
}

/// Where one direction's frames end, as its reads come: the bot's frames go to the game server between two of the
/// client's, never inside one, as the C# version wrote its own (inferred, `main:docs/client-game.md`, D61, 3.7.12.12:
/// 18 requests of its own, each whole between the client's frames). Keeps only the header in progress and how much of
/// the body is left.
#[derive(Debug, Default)]
pub(crate) struct Ends {
    header: Vec<u8>,
    body: usize,
}

impl Ends {
    /// Whether the bytes taken so far end on a frame's end.
    pub(crate) fn between(&self) -> bool {
        self.header.is_empty() && self.body == 0
    }

    /// Takes the bytes of one read: how many of them come before the first frame's end they hold, if they hold one.
    /// After an error the stream is no longer in frames.
    pub(crate) fn push(&mut self, bytes: &[u8]) -> Result<Option<usize>, RelayError> {
        let mut first = None;
        let mut at = 0;
        while at < bytes.len() {
            if self.body > 0 {
                let taken = self.body.min(bytes.len() - at);
                self.body -= taken;
                at += taken;
            } else {
                self.header.push(bytes[at]);
                at += 1;
                let Some((_, body)) = header(&self.header)? else {
                    continue;
                };
                self.header.clear();
                self.body = body;
            }
            if self.between() && first.is_none() {
                first = Some(at);
            }
        }
        Ok(first)
    }
}

/// Gives `each` the body of every whole frame at the start of `bytes`; how many bytes they took.
fn split(mut bytes: &[u8], each: &mut impl FnMut(&[u8])) -> Result<usize, RelayError> {
    let start = bytes.len();
    while let Some((header, body)) = header(bytes)? {
        let Some(frame) = bytes.get(header..header + body) else {
            break;
        };
        each(frame);
        bytes = &bytes[header + body..];
    }
    Ok(start - bytes.len())
}

/// The length of the whole frame at the start of `bytes`, header included: every byte of both connections is in frames
/// of a protobuf varint length, at most 5 bytes, then that many bytes (inferred, `main:docs/client.md`, D38,
/// 3.7.12.12 and 3.7.13.17). `None` while the frame is not whole yet.
#[cfg_attr(
    not(test),
    expect(
        dead_code,
        reason = "the bot's frames are checked with it, which nothing sends before step 4's R1"
    )
)]
pub(crate) fn whole(bytes: &[u8]) -> Result<Option<usize>, RelayError> {
    let Some((header, body)) = header(bytes)? else {
        return Ok(None);
    };
    let total = header + body;
    Ok((bytes.len() >= total).then_some(total))
}

/// The frame's header length and body length, once the header is whole.
pub(crate) fn header(bytes: &[u8]) -> Result<Option<(usize, usize)>, RelayError> {
    let mut length: u64 = 0;
    for (index, &byte) in bytes.iter().enumerate() {
        if index == 5 {
            return Err(RelayError::FrameHeader);
        }
        length |= u64::from(byte & 0x7f) << (7 * index);
        if byte & 0x80 == 0 {
            let body = usize::try_from(length).unwrap_or(usize::MAX);
            if body > LIMIT {
                return Err(RelayError::FrameTooLarge {
                    length: body,
                    limit: LIMIT,
                });
            }
            return Ok(Some((index + 1, body)));
        }
    }
    if bytes.len() >= 5 {
        return Err(RelayError::FrameHeader);
    }
    Ok(None)
}

/// Appends `body` to `out` as a frame.
pub(crate) fn encode(body: &[u8], out: &mut Vec<u8>) {
    wire::write_varint(body.len() as u64, out);
    out.extend_from_slice(body);
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ends_of(reads: &[&[u8]]) -> Vec<Option<usize>> {
        let mut ends = Ends::default();
        reads
            .iter()
            .map(|read| ends.push(read).unwrap_or_else(|error| panic!("{error}")))
            .collect()
    }

    #[test]
    fn finds_where_the_first_frame_of_a_read_ends() {
        let mut bytes = Vec::new();
        encode(b"one", &mut bytes);
        encode(b"two", &mut bytes);

        assert_eq!(ends_of(&[&bytes]), [Some(4)]);
        assert_eq!(
            ends_of(&[&bytes[..2], &bytes[2..6], &bytes[6..]]),
            [None, Some(2), Some(2)]
        );
    }

    #[test]
    fn a_header_split_across_reads_and_an_empty_frame_end_where_they_end() {
        let mut long = Vec::new();
        encode(&[7; 300], &mut long);

        assert_eq!(ends_of(&[&long[..1], &long[1..2], &long[2..]]), [None, None, Some(300)]);
        assert_eq!(ends_of(&[&[0]]), [Some(1)]);
    }

    #[test]
    fn is_between_frames_only_once_a_frame_ended() {
        let mut ends = Ends::default();
        assert!(ends.between());
        let _ = ends.push(&[3, b'a']);
        assert!(!ends.between());
        let _ = ends.push(b"bc");
        assert!(ends.between());
        let _ = ends.push(&[0x80]);
        assert!(!ends.between(), "a header in progress");
    }

    #[test]
    fn bytes_no_longer_in_frames_are_an_error() {
        let mut ends = Ends::default();

        assert!(matches!(ends.push(&[0x80; 6]), Err(RelayError::FrameHeader)));
    }

    #[test]
    fn waits_for_the_whole_frame() {
        let mut frame = Vec::new();
        encode(&[7; 300], &mut frame);

        assert_eq!(whole(&frame[..1]).unwrap_or_else(|error| panic!("{error}")), None);
        assert_eq!(whole(&frame[..2]).unwrap_or_else(|error| panic!("{error}")), None);
        assert_eq!(whole(&frame[..301]).unwrap_or_else(|error| panic!("{error}")), None);
        assert_eq!(whole(&frame).unwrap_or_else(|error| panic!("{error}")), Some(302));
    }

    #[test]
    fn finds_the_first_of_several_frames() {
        let mut bytes = Vec::new();
        encode(b"one", &mut bytes);
        encode(b"two", &mut bytes);

        assert_eq!(whole(&bytes).unwrap_or_else(|error| panic!("{error}")), Some(4));
    }

    #[test]
    fn reads_an_empty_frame() {
        assert_eq!(whole(&[0]).unwrap_or_else(|error| panic!("{error}")), Some(1));
        assert_eq!(whole(&[]).unwrap_or_else(|error| panic!("{error}")), None);
    }

    #[test]
    fn accepts_a_frame_of_exactly_the_limit() {
        let mut header = Vec::new();
        wire::write_varint(LIMIT as u64, &mut header);

        let read = header_of(&header);

        assert_eq!(read, Some((header.len(), LIMIT)));
    }

    #[test]
    fn refuses_a_frame_over_the_limit() {
        let mut header = Vec::new();
        wire::write_varint(LIMIT as u64 + 1, &mut header);

        let error = super::header(&header).err();

        assert!(
            matches!(error, Some(RelayError::FrameTooLarge { length, .. }) if length == LIMIT + 1),
            "{error:?}"
        );
    }

    #[test]
    fn refuses_five_header_bytes_that_go_on() {
        let error = super::header(&[0x80; 5]).err();

        assert!(matches!(error, Some(RelayError::FrameHeader)), "{error:?}");
    }

    #[test]
    fn refuses_a_sixth_header_byte() {
        let error = super::header(&[0x80, 0x80, 0x80, 0x80, 0x80, 0x01]).err();

        assert!(matches!(error, Some(RelayError::FrameHeader)), "{error:?}");
    }

    /// The bodies `frames` gives for `reads`, or the first error.
    fn pushed(frames: &mut Frames, reads: &[&[u8]]) -> Result<Vec<Vec<u8>>, RelayError> {
        let mut bodies = Vec::new();
        for read in reads {
            frames.push(read, |body| bodies.push(body.to_vec()))?;
        }
        Ok(bodies)
    }

    #[test]
    fn gives_a_frame_whose_header_is_cut_between_reads() {
        let mut bytes = Vec::new();
        encode(&[7; 300], &mut bytes);

        let bodies = pushed(&mut Frames::default(), &[&bytes[..1], &bytes[1..]]);

        assert_eq!(bodies.ok(), Some(vec![vec![7; 300]]));
    }

    #[test]
    fn gives_every_frame_of_one_read() {
        let mut bytes = Vec::new();
        encode(b"one", &mut bytes);
        encode(b"", &mut bytes);
        encode(b"three", &mut bytes);

        let bodies = pushed(&mut Frames::default(), &[&bytes]);

        assert_eq!(bodies.ok(), Some(vec![b"one".to_vec(), Vec::new(), b"three".to_vec()]));
    }

    #[test]
    fn gives_the_largest_frame_seen_over_eleven_reads() {
        let body: Vec<u8> = (0..88_581_u32).map(|index| (index % 251) as u8).collect();
        let mut bytes = Vec::new();
        encode(&body, &mut bytes);
        encode(b"next", &mut bytes);
        let reads: Vec<&[u8]> = bytes.chunks(bytes.len().div_ceil(11)).collect();
        assert_eq!(reads.len(), 11);
        let mut frames = Frames::default();
        let mut bodies = Vec::new();

        for (index, read) in reads.iter().enumerate() {
            let before = bodies.len();
            frames
                .push(read, |body| bodies.push(body.to_vec()))
                .unwrap_or_else(|error| panic!("{error}"));
            // Nothing comes before the read that completes the frame.
            assert_eq!(bodies.len() > before, index == 10, "read {index}");
        }

        assert_eq!(bodies, vec![body, b"next".to_vec()]);
        assert_eq!(frames.pending, Vec::<u8>::new());
    }

    #[test]
    fn keeps_only_what_is_not_whole_yet() {
        let mut bytes = Vec::new();
        encode(b"one", &mut bytes);
        encode(b"two", &mut bytes);
        let mut frames = Frames::default();

        let first = pushed(&mut frames, &[&bytes[..6]]);
        let pending = frames.pending.len();
        let second = pushed(&mut frames, &[&bytes[6..]]);

        assert_eq!(first.ok(), Some(vec![b"one".to_vec()]));
        assert_eq!(pending, 2);
        assert_eq!(second.ok(), Some(vec![b"two".to_vec()]));
        assert_eq!(frames.pending, Vec::<u8>::new());
    }

    #[test]
    fn gives_a_frame_ending_in_the_read_that_starts_the_next() {
        let mut bytes = Vec::new();
        encode(b"first", &mut bytes);
        encode(b"second", &mut bytes);

        let bodies = pushed(&mut Frames::default(), &[&bytes[..3], &bytes[3..9], &bytes[9..]]);

        assert_eq!(bodies.ok(), Some(vec![b"first".to_vec(), b"second".to_vec()]));
    }

    #[test]
    fn refuses_a_sixth_header_byte_after_whole_frames() {
        let mut bytes = Vec::new();
        encode(b"one", &mut bytes);
        let mut frames = Frames::default();
        let mut bodies = Vec::new();

        let result = frames.push(&bytes, |body| bodies.push(body.to_vec()));
        let error = frames.push(&[0x80; 6], |body| bodies.push(body.to_vec())).err();

        assert!(result.is_ok());
        assert!(matches!(error, Some(RelayError::FrameHeader)), "{error:?}");
        assert_eq!(bodies, vec![b"one".to_vec()]);
    }

    #[test]
    fn refuses_a_frame_over_the_limit_cut_between_reads() {
        let mut header = Vec::new();
        wire::write_varint(LIMIT as u64 + 1, &mut header);

        let error = pushed(&mut Frames::default(), &[&header[..1], &header[1..]]).err();

        assert!(matches!(error, Some(RelayError::FrameTooLarge { .. })), "{error:?}");
    }

    fn header_of(bytes: &[u8]) -> Option<(usize, usize)> {
        super::header(bytes).unwrap_or_else(|error| panic!("{error}"))
    }
}
