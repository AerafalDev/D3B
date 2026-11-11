use std::path::PathBuf;

/// Why the relay cannot redirect the client or carry a connection. Each message names what the player or a bug report
/// needs; none holds a login token, a ticket or a frame's content.
#[derive(Debug, thiserror::Error)]
pub enum RelayError {
    #[error("the client's version file is {size} bytes, more than the {limit} it may hold")]
    VersionTooLarge { size: usize, limit: usize },
    #[error("the client's version file has no ConfigUrl line")]
    NoConfigUrl,
    #[error("the client's version file has more than one ConfigUrl line")]
    ConfigUrlTwice,
    #[error("the ConfigUrl of the client's version file is not text")]
    ConfigUrlNotText,
    #[error("Ankama's configuration is {size} bytes, more than the {limit} it may hold")]
    ConfigTooLarge { size: usize, limit: usize },
    #[error("Ankama's configuration is not a JSON object: {0}")]
    ConfigNotJson(String),
    #[error("Ankama's configuration has no connectionHosts list of texts")]
    NoConnectionHosts,
    #[error("Ankama's configuration lists {0} connection hosts, where D3B relays exactly one")]
    ConnectionHosts(usize),
    #[error("the connection host '{0}' is not <name>:<host>:<ports>")]
    ConnectionHostMalformed(String),
    #[error(
        "Ankama's configuration sends clients to {0}, a loopback address, not Ankama's: repair the client from the launcher"
    )]
    AlreadyRedirected(String),
    #[error(
        "the client's version file names D3B's configuration, and Ankama's URL is lost: repair the client from the launcher"
    )]
    OriginalLost,
    #[error(
        "the client's version file names a configuration neither Ankama's nor D3B's: repair the client from the launcher"
    )]
    RedirectedElsewhere,
    #[error("Ankama's configuration cannot be downloaded, and none is kept from an earlier start")]
    NoAnkamaConfig,
    #[error("the client's version file changed while D3B redirected it")]
    VersionChanged,
    #[error("cannot read or write {path}: {source}")]
    Io {
        path: PathBuf,
        #[source]
        source: std::io::Error,
    },
}
