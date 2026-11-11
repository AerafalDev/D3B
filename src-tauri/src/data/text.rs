use serde::Deserialize;

/// One text of the client in the five languages the data folder holds (ADR 0008).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Text {
    pub fr: String,
    pub en: String,
    pub de: String,
    pub es: String,
    pub pt: String,
}
