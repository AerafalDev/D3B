//! A build's pins (`docs/protocol.md`, "Decoding", ADR 0012): our readable names paired with the build's obfuscated
//! ones, for the messages a feature reads, and the names of the build's messages that carry a session secret. They are
//! ours, committed in `src-tauri/protocol/pins/<build>.json` and embedded in the app; a build without them is unmapped.

use std::collections::{HashMap, HashSet};
use std::fmt;

use serde::Deserialize;
use serde::de::{Deserializer, MapAccess, Visitor};

use super::message::READABLE;

/// The pins embedded in the app: one file per build, named after it.
const EMBEDDED: &[(&str, &str)] = &[("3.7.13.17", include_str!("../../protocol/pins/3.7.13.17.json"))];

/// The longest name a build gives a message, as `protocol::envelope` reads it; 3.7.13.17's are three letters.
const NAME_LIMIT: usize = 8;

/// A build's pins, checked.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Pins {
    /// The SHA-256 of the build's schema, as the extractor gives it: the audit test checks it.
    schema_sha256: String,
    /// The readable name of each pinned name of the build.
    readable: HashMap<String, &'static str>,
    secrets: HashSet<String>,
}

/// Pins that cannot be used, with the build they are for and why.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum PinsError {
    #[error("the pins of {build} are not of their shape: {reason}")]
    Shape { build: String, reason: String },
    #[error("the pins of {build} name build {named}")]
    Build { build: String, named: String },
    #[error("the pins of {build} give a schema digest that is no SHA-256")]
    Digest { build: String },
    #[error("the pins of {build} pin {readable}, which is no message of ours")]
    NotOurs { build: String, readable: String },
    #[error("the pins of {build} give {name}, which is not a name of a build's message")]
    Name { build: String, name: String },
    #[error("the pins of {build} pin {name} twice")]
    Twice { build: String, name: String },
    #[error("the pins of {build} pin {name}, which is secret")]
    Secret { build: String, name: String },
}

/// The file's shape: `{build, schemaSha256, messages: {Readable: "abc"}, secrets: ["abc"]}`.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct File {
    build: String,
    schema_sha256: String,
    /// In the file's order and with every pair, a readable name given twice included: a map would keep the last.
    #[serde(deserialize_with = "pairs")]
    messages: Vec<(String, String)>,
    secrets: Vec<String>,
}

impl Pins {
    /// The pins the app embeds for `build`, checked; `None` when it has none for it.
    pub(crate) fn embedded(build: &str) -> Option<Result<Self, PinsError>> {
        EMBEDDED
            .iter()
            .find(|(pinned, _)| *pinned == build)
            .map(|(pinned, json)| Self::parse(pinned, json))
    }

    /// The pins of `build` read from `json` and checked: the file names its build and a SHA-256, every readable name
    /// is one of [`READABLE`], every name is shaped as a build's, none is pinned twice, none both pinned and secret.
    pub(crate) fn parse(build: &str, json: &str) -> Result<Self, PinsError> {
        let build = build.to_owned();
        let file: File = serde_json::from_str(json).map_err(|error| PinsError::Shape {
            build: build.clone(),
            reason: error.to_string(),
        })?;
        if file.build != build {
            return Err(PinsError::Build {
                build,
                named: file.build,
            });
        }
        let digest = &file.schema_sha256;
        if digest.len() != 64 || !digest.bytes().all(|byte| matches!(byte, b'0'..=b'9' | b'a'..=b'f')) {
            return Err(PinsError::Digest { build });
        }
        if let Some(name) = file.secrets.iter().find(|name| !is_name(name)) {
            return Err(PinsError::Name {
                build,
                name: name.clone(),
            });
        }
        let secrets: HashSet<String> = file.secrets.into_iter().collect();
        let mut readable = HashMap::new();
        let mut pinned = HashSet::new();
        for (ours, name) in file.messages {
            let Some(&ours) = READABLE.iter().find(|&&known| known == ours) else {
                return Err(PinsError::NotOurs { build, readable: ours });
            };
            if !pinned.insert(ours) {
                return Err(PinsError::Twice {
                    build,
                    name: ours.to_owned(),
                });
            }
            if !is_name(&name) {
                return Err(PinsError::Name { build, name });
            }
            if secrets.contains(&name) {
                return Err(PinsError::Secret { build, name });
            }
            if readable.contains_key(&name) {
                return Err(PinsError::Twice { build, name });
            }
            readable.insert(name, ours);
        }
        Ok(Self {
            schema_sha256: file.schema_sha256,
            readable,
            secrets,
        })
    }

    /// The builds the app embeds pins for.
    #[cfg(test)]
    pub(crate) fn builds() -> impl Iterator<Item = &'static str> {
        EMBEDDED.iter().map(|(build, _)| *build)
    }

    /// The SHA-256 of the build's schema the pins were made against, as the extractor gives it.
    #[cfg(test)]
    pub(crate) fn schema_sha256(&self) -> &str {
        &self.schema_sha256
    }

    /// Each pinned pair: our readable name, and the build's name.
    #[cfg(test)]
    pub(crate) fn pinned(&self) -> impl Iterator<Item = (&'static str, &str)> {
        self.readable.iter().map(|(name, readable)| (*readable, name.as_str()))
    }

    /// Whether `name`, a message's name in the build, carries a session secret.
    pub(crate) fn is_secret(&self, name: &str) -> bool {
        self.secrets.contains(name)
    }

    /// The readable name pinned to `name`, a message's name in the build; never a secret's, which [`Pins::parse`]
    /// refuses to pin, so that a secret's bytes are never decoded.
    pub(crate) fn readable(&self, name: &str) -> Option<&'static str> {
        self.readable.get(name).copied()
    }
}

/// Reads a JSON object as its pairs, in order, every one kept.
fn pairs<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Vec<(String, String)>, D::Error> {
    struct Pairs;

    impl<'de> Visitor<'de> for Pairs {
        type Value = Vec<(String, String)>;

        fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
            formatter.write_str("an object of readable names to a build's names")
        }

        fn visit_map<A: MapAccess<'de>>(self, mut map: A) -> Result<Self::Value, A::Error> {
            let mut pairs = Vec::new();
            while let Some(pair) = map.next_entry()? {
                pairs.push(pair);
            }
            Ok(pairs)
        }
    }

    deserializer.deserialize_map(Pairs)
}

/// Whether `name` is shaped as a build's message name: 1 to [`NAME_LIMIT`] lowercase letters.
fn is_name(name: &str) -> bool {
    (1..=NAME_LIMIT).contains(&name.len()) && name.bytes().all(|byte| byte.is_ascii_lowercase())
}

#[cfg(test)]
mod tests {
    use super::*;

    const DIGEST: &str = "d74dfab609f87963fdd6230f5936c547da06c6deff9873cdfd75915d0e9b4980";

    fn pins(build: &str, digest: &str, messages: &str, secrets: &str) -> String {
        format!(
            r#"{{"build": "{build}", "schemaSha256": "{digest}", "messages": {{{messages}}}, "secrets": [{secrets}]}}"#
        )
    }

    fn parsed(messages: &str, secrets: &str) -> Result<Pins, PinsError> {
        Pins::parse("3.7.13.17", &pins("3.7.13.17", DIGEST, messages, secrets))
    }

    #[test]
    fn the_pins_of_3_7_13_17_pin_every_message_of_ours() {
        let pins = Pins::embedded("3.7.13.17").map(|pins| pins.unwrap_or_else(|error| panic!("{error}")));
        let pins = pins.unwrap_or_else(|| panic!("no pins of 3.7.13.17"));

        let mut pinned: Vec<&str> = pins.readable.values().copied().collect();
        pinned.sort_unstable();
        let mut ours = READABLE.to_vec();
        ours.sort_unstable();
        assert_eq!(pinned, ours);
        assert_eq!(pins.readable("jow"), Some("MapCurrentEvent"));
        let mut secrets: Vec<&str> = pins.secrets.iter().map(String::as_str).collect();
        secrets.sort_unstable();
        // The ticket's request (verified), the four the study found holding session secrets, and `koh`, whose frames
        // each hold a distinct hexadecimal string (inferred, `artifacts/studies/live-view.md`).
        assert_eq!(secrets, ["knc", "koc", "kod", "kof", "koh", "kso"]);
        assert_eq!(pins.readable("kqp"), Some("CharacterLevelUpEvent"));
        assert_eq!(pins.readable("ith"), Some("KamasUpdateEvent"));
        assert_eq!(pins.schema_sha256, DIGEST);
    }

    #[test]
    fn a_build_without_pins_has_none() {
        assert!(Pins::embedded("3.7.14.1").is_none());
        assert!(Pins::embedded("").is_none());
    }

    #[test]
    fn reads_a_name_pinned_and_a_secret() {
        let pins = parsed(r#""MapCurrentEvent": "jow""#, r#""knc""#).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(pins.readable("jow"), Some("MapCurrentEvent"));
        assert_eq!(pins.readable("knc"), None);
        assert!(pins.secrets.contains("knc"));
        assert!(!pins.secrets.contains("jow"));
    }

    #[test]
    fn refuses_pins_not_of_their_shape() {
        let unknown_key = r#"{"build": "3.7.13.17", "schemaSha256": "", "messages": {}, "secrets": [], "more": 1}"#;
        for json in ["", "[]", r#"{"build": "3.7.13.17"}"#, unknown_key] {
            assert!(
                matches!(Pins::parse("3.7.13.17", json), Err(PinsError::Shape { .. })),
                "{json}"
            );
        }
    }

    #[test]
    fn refuses_pins_of_another_build() {
        assert_eq!(
            Pins::parse("3.7.13.17", &pins("3.7.13.18", DIGEST, "", "")),
            Err(PinsError::Build {
                build: "3.7.13.17".into(),
                named: "3.7.13.18".into()
            })
        );
    }

    #[test]
    fn refuses_a_digest_that_is_no_sha256() {
        for digest in ["", &DIGEST[1..], &DIGEST.to_uppercase(), &format!("{}g", &DIGEST[1..])] {
            assert!(
                matches!(
                    Pins::parse("3.7.13.17", &pins("3.7.13.17", digest, "", "")),
                    Err(PinsError::Digest { .. })
                ),
                "{digest}"
            );
        }
    }

    #[test]
    fn refuses_a_readable_name_that_is_not_ours() {
        assert!(matches!(
            parsed(r#""MapCurrentEvents": "jow""#, ""),
            Err(PinsError::NotOurs { readable, .. }) if readable == "MapCurrentEvents"
        ));
    }

    #[test]
    fn refuses_a_name_not_shaped_as_a_builds() {
        for name in ["", "JOW", "abcdefghi", "j.w"] {
            let pinned = format!(r#""MapCurrentEvent": "{name}""#);

            assert!(matches!(parsed(&pinned, ""), Err(PinsError::Name { .. })), "{name}");
            assert!(
                matches!(parsed("", &format!(r#""{name}""#)), Err(PinsError::Name { .. })),
                "{name}"
            );
        }
        assert!(parsed(r#""MapCurrentEvent": "abcdefgh""#, r#""k""#).is_ok());
    }

    #[test]
    fn refuses_a_name_pinned_twice() {
        assert!(matches!(
            parsed(r#""MapCurrentEvent": "jow", "MapInformationRequest": "jow""#, ""),
            Err(PinsError::Twice { name, .. }) if name == "jow"
        ));
    }

    #[test]
    fn refuses_a_readable_name_given_twice() {
        assert!(matches!(
            parsed(r#""MapCurrentEvent": "jow", "MapCurrentEvent": "jnj""#, ""),
            Err(PinsError::Twice { name, .. }) if name == "MapCurrentEvent"
        ));
    }

    #[test]
    fn refuses_a_secret_pinned() {
        assert!(matches!(
            parsed(r#""MapCurrentEvent": "knc""#, r#""knc""#),
            Err(PinsError::Secret { name, .. }) if name == "knc"
        ));
    }
}
