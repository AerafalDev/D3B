use super::RelayError;
use super::wire;

/// The most a frame may hold. The largest frame seen from a game server is 80,966 bytes (inferred,
/// `main:docs/client-game.md`, 3.7.12.12); the C# reader bounded frames at 16 MiB too.
pub(crate) const LIMIT: usize = 16 * 1024 * 1024;

/// The length of the whole frame at the start of `bytes`, header included: every byte of both connections is in frames
/// of a protobuf varint length, at most 5 bytes, then that many bytes (inferred, `main:docs/client.md`, D38,
/// 3.7.12.12 and 3.7.13.17). `None` while the frame is not whole yet.
#[cfg_attr(
    not(test),
    expect(dead_code, reason = "the capture replay reads frames with it, and the protocol will")
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

    fn header_of(bytes: &[u8]) -> Option<(usize, usize)> {
        super::header(bytes).unwrap_or_else(|error| panic!("{error}"))
    }
}
