use std::ops::Range;

use super::RelayError;

/// The client's `Dofus_Data/StreamingAssets/version` file, whose `ConfigUrl` line names the configuration the client
/// loads at start. Three `Key=Value` lines ending in CRLF, 113 bytes (verified, 3.7.13.17). Only the `ConfigUrl`
/// value ever changes: every other byte, line ends included, is written back as it was read.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct VersionFile {
    bytes: Vec<u8>,
    /// Where the `ConfigUrl` value lies in `bytes`, spaces around it and the line end excluded.
    value: Range<usize>,
}

impl VersionFile {
    /// The most the file may hold; the real one is 113 bytes, so anything larger is not the client's.
    pub(crate) const LIMIT: usize = 4096;

    const KEY: &'static [u8] = b"ConfigUrl";

    /// Reads the file's bytes, finding its one `ConfigUrl` line.
    pub(crate) fn parse(bytes: Vec<u8>) -> Result<Self, RelayError> {
        if bytes.len() > Self::LIMIT {
            return Err(RelayError::VersionTooLarge {
                size: bytes.len(),
                limit: Self::LIMIT,
            });
        }
        let mut value = None;
        let mut start = 0;
        for line in bytes.split_inclusive(|&byte| byte == b'\n') {
            if let Some(found) = config_url_value(line) {
                if value.is_some() {
                    return Err(RelayError::ConfigUrlTwice);
                }
                value = Some(start + found.start..start + found.end);
            }
            start += line.len();
        }
        let value = value.ok_or(RelayError::NoConfigUrl)?;
        if std::str::from_utf8(&bytes[value.clone()]).is_err() {
            return Err(RelayError::ConfigUrlNotText);
        }
        Ok(Self { bytes, value })
    }

    /// The configuration URL the client loads.
    pub(crate) fn config_url(&self) -> &str {
        // `parse` checked that the value is UTF-8, so this never falls back.
        std::str::from_utf8(&self.bytes[self.value.clone()]).unwrap_or_default()
    }

    /// The file's bytes with `url` as its `ConfigUrl`, every other byte kept.
    pub(crate) fn with_config_url(&self, url: &str) -> Vec<u8> {
        let mut bytes = Vec::with_capacity(self.bytes.len() + url.len());
        bytes.extend_from_slice(&self.bytes[..self.value.start]);
        bytes.extend_from_slice(url.as_bytes());
        bytes.extend_from_slice(&self.bytes[self.value.end..]);
        bytes
    }
}

/// Where the value of `line` lies when it is the `ConfigUrl` line, relative to the line, spaces trimmed.
fn config_url_value(line: &[u8]) -> Option<Range<usize>> {
    let separator = line.iter().position(|&byte| byte == b'=')?;
    if line[..separator].trim_ascii() != VersionFile::KEY {
        return None;
    }
    // Trimmed at the end first, so that an empty value never lets the line end count as leading spaces.
    let value = line[separator + 1..].trim_ascii_end();
    let leading = value.len() - value.trim_ascii_start().len();
    Some(separator + 1 + leading..separator + 1 + value.len())
}

#[cfg(test)]
mod tests {
    use super::*;

    const CRLF: &[u8] =
        b"Version=3.7.13.17\r\nBuildDate=2026-11-03T14:09:52+00:00\r\nConfigUrl=https://example.com/config.json\r\n";

    fn parse(bytes: &[u8]) -> Result<VersionFile, RelayError> {
        VersionFile::parse(bytes.to_vec())
    }

    #[test]
    fn writes_an_empty_value_on_its_own_line() {
        for (bytes, expected) in [
            (
                &b"ConfigUrl=
Version=1
"[..],
                &b"ConfigUrl=x
Version=1
"[..],
            ),
            (
                b"ConfigUrl=   
Version=1
",
                b"ConfigUrl=x   
Version=1
",
            ),
        ] {
            let file = parse(bytes).unwrap_or_else(|error| panic!("{error}"));

            assert_eq!(file.config_url(), "");
            assert_eq!(file.with_config_url("x"), expected);
        }
    }

    #[test]
    fn keeps_an_equals_sign_inside_the_url() {
        let file = parse(b"ConfigUrl=https://cdn.example.com/x.json?v=1\r\n").unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(file.config_url(), "https://cdn.example.com/x.json?v=1");
    }

    #[test]
    fn accepts_a_file_of_exactly_the_limit() {
        let mut bytes = b"ConfigUrl=a\n".to_vec();
        bytes.resize(VersionFile::LIMIT, b' ');

        assert!(parse(&bytes).is_ok());
    }

    #[test]
    fn reads_the_config_url() {
        let file = parse(CRLF).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(file.config_url(), "https://example.com/config.json");
    }

    #[test]
    fn rewrites_only_the_config_url_keeping_crlf() {
        let file = parse(CRLF).unwrap_or_else(|error| panic!("{error}"));

        let written = file.with_config_url("file:///C:/D3B/config.json");

        assert_eq!(
            written,
            b"Version=3.7.13.17\r\nBuildDate=2026-11-03T14:09:52+00:00\r\nConfigUrl=file:///C:/D3B/config.json\r\n"
        );
    }

    #[test]
    fn keeps_lf_and_a_last_line_without_end() {
        let file = parse(b"ConfigUrl=https://a\nVersion=1").unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(file.with_config_url("x"), b"ConfigUrl=x\nVersion=1");

        let file = parse(b"Version=1\nConfigUrl = https://a ").unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(file.config_url(), "https://a");
        assert_eq!(file.with_config_url("x"), b"Version=1\nConfigUrl = x ");
    }

    #[test]
    fn a_written_file_reads_back() {
        let written = parse(CRLF)
            .unwrap_or_else(|error| panic!("{error}"))
            .with_config_url("file:///C:/x.json");

        assert_eq!(
            parse(&written).unwrap_or_else(|error| panic!("{error}")).config_url(),
            "file:///C:/x.json"
        );
    }

    #[test]
    fn refuses_a_file_without_config_url() {
        let error = parse(b"Version=3.7.13.17\r\nConfigUrlX=a\r\n")
            .err()
            .unwrap_or_else(|| panic!("expected an error"));

        assert!(matches!(error, RelayError::NoConfigUrl), "{error}");
    }

    #[test]
    fn refuses_config_url_twice() {
        let error = parse(b"ConfigUrl=a\r\nConfigUrl=b\r\n")
            .err()
            .unwrap_or_else(|| panic!("expected an error"));

        assert!(matches!(error, RelayError::ConfigUrlTwice), "{error}");
    }

    #[test]
    fn refuses_a_config_url_that_is_not_text() {
        let error = parse(b"ConfigUrl=\xff\r\n")
            .err()
            .unwrap_or_else(|| panic!("expected an error"));

        assert!(matches!(error, RelayError::ConfigUrlNotText), "{error}");
    }

    #[test]
    fn refuses_a_file_over_the_limit() {
        let mut bytes = b"ConfigUrl=a\n".to_vec();
        bytes.resize(VersionFile::LIMIT + 1, b' ');

        let error = parse(&bytes).err().unwrap_or_else(|| panic!("expected an error"));

        assert!(
            matches!(
                error,
                RelayError::VersionTooLarge {
                    size: 4097,
                    limit: 4096
                }
            ),
            "{error}"
        );
    }
}
