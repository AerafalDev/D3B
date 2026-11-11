use std::fmt::Write as _;
use std::path::{Path, PathBuf};
use std::time::Duration;

use super::ankama_config::{AnkamaConfig, ConnectionHost};
use super::version_file::VersionFile;
use super::{ADDRESS, RelayError};

/// The client's redirect to the relay, for the app's life (`docs/relay.md`, "The redirect"; inferred,
/// `main:docs/client.md`, D35, measured on 3.7.12.12 and 3.7.13.17 by the owner).
///
/// The client's `version` file names Ankama's configuration; while redirected, it names D3B's copy, a `file:` URL,
/// whose connection host is the relay. The state, outside the client and outside D3B's install folder:
/// `config-url` (Ankama's URL), `ankama-config.json` (the last configuration downloaded, used when Ankama cannot be
/// reached) and `config.json` (the copy the client reads).
#[derive(Debug, Clone)]
pub(crate) struct Redirect {
    version: PathBuf,
    state: PathBuf,
}

/// What [`Redirect::original_url`] found in the `version` file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Original {
    /// Ankama's configuration URL, to download.
    pub(crate) url: String,
}

/// What [`Redirect::restore`] did.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Restored {
    /// Ankama's URL is back in the `version` file.
    Restored,
    /// The line was no longer D3B's (the launcher rewrote it, or it was never redirected): left alone.
    NotOurs,
}

impl Redirect {
    const ORIGINAL: &'static str = "config-url";
    const ANKAMA: &'static str = "ankama-config.json";
    const COPY: &'static str = "config.json";

    /// The redirect of the client installed in `client` (its folder holding `Dofus_Data`), its state in `state`.
    pub(crate) fn new(client: &Path, state: PathBuf) -> Self {
        Self {
            version: client.join("Dofus_Data").join("StreamingAssets").join("version"),
            state,
        }
    }

    /// The `file:` URL of D3B's copy, which the `version` file names while redirected.
    pub(crate) fn copy_url(&self) -> String {
        file_url(&self.state.join(Self::COPY))
    }

    /// Ankama's configuration URL: the `version` file's when it is an `https:` URL, the kept one when the line is
    /// already D3B's (after a crash). Any other URL is another program's redirect (or the C# D3B's): refused, since
    /// putting it back at quit would leave the client on a configuration nobody serves.
    pub(crate) fn original_url(&self) -> Result<Original, RelayError> {
        let url = self.read_version()?.config_url().to_owned();
        if url == self.copy_url() {
            let kept = self.state.join(Self::ORIGINAL);
            return match std::fs::read_to_string(&kept) {
                Ok(url) if url.starts_with("https:") => Ok(Original { url }),
                Ok(_) => Err(RelayError::OriginalLost),
                Err(error) if error.kind() == std::io::ErrorKind::NotFound => Err(RelayError::OriginalLost),
                Err(source) => Err(RelayError::Io { path: kept, source }),
            };
        }
        if !url.starts_with("https:") {
            return Err(RelayError::RedirectedElsewhere);
        }
        Ok(Original { url })
    }

    /// Redirects the client: writes the state, then points `version` at D3B's copy. `downloaded` is Ankama's
    /// configuration just read from `original`, or `None` when it could not be reached, in which case the kept one is
    /// used. Nothing is touched when neither can be read. Returns Ankama's connection host, the one the relay
    /// connects to.
    pub(crate) fn apply(&self, original: &Original, downloaded: Option<&str>) -> Result<ConnectionHost, RelayError> {
        let kept = self.state.join(Self::ANKAMA);
        let text = match downloaded {
            Some(text) => text.to_owned(),
            None => match std::fs::read_to_string(&kept) {
                Ok(text) => text,
                Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                    return Err(RelayError::NoAnkamaConfig);
                }
                Err(source) => return Err(RelayError::Io { path: kept, source }),
            },
        };
        let config = AnkamaConfig::parse(&text)?;

        // Read again: the launcher may have rewritten the file while the configuration downloaded.
        let version = self.read_version()?;
        let ours = self.copy_url();
        if version.config_url() != original.url && version.config_url() != ours {
            return Err(RelayError::VersionChanged);
        }

        std::fs::create_dir_all(&self.state).map_err(|source| RelayError::Io {
            path: self.state.clone(),
            source,
        })?;
        write(&self.state.join(Self::ORIGINAL), original.url.as_bytes())?;
        if downloaded.is_some() {
            write(&kept, text.as_bytes())?;
        }
        write(&self.state.join(Self::COPY), config.redirected_to(ADDRESS).as_bytes())?;
        if version.config_url() != ours {
            write(&self.version, &version.with_config_url(&ours))?;
        }
        Ok(config.connection_host().clone())
    }

    /// Whether the `version` file still names D3B's copy; `false` once the launcher rewrote it (an update or a
    /// repair, measured by the owner), which calls for applying again.
    pub(crate) fn is_applied(&self) -> Result<bool, RelayError> {
        Ok(self.read_version()?.config_url() == self.copy_url())
    }

    /// Puts Ankama's URL back, only if the line is still D3B's.
    pub(crate) fn restore(&self) -> Result<Restored, RelayError> {
        let version = self.read_version()?;
        if version.config_url() != self.copy_url() {
            return Ok(Restored::NotOurs);
        }
        let original = self.original_url()?;
        write(&self.version, &version.with_config_url(&original.url))?;
        Ok(Restored::Restored)
    }

    fn read_version(&self) -> Result<VersionFile, RelayError> {
        use std::io::Read;

        let io = |source| RelayError::Io {
            path: self.version.clone(),
            source,
        };
        // One byte past the limit is enough for the parser to refuse a file that is too large.
        let mut bytes = Vec::new();
        std::fs::File::open(&self.version)
            .map_err(io)?
            .take(VersionFile::LIMIT as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(io)?;
        VersionFile::parse(bytes)
    }
}

/// Downloads Ankama's configuration, at most [`AnkamaConfig::LIMIT`] bytes; `None` when it cannot be reached, so
/// that the kept copy is used. Network only: not unit tested, measured in game (`docs/relay.md`).
#[cfg_attr(
    test,
    expect(
        dead_code,
        reason = "it needs Ankama's server, so the app uses it and the owner measures it"
    )
)]
pub(crate) async fn download(url: &str) -> Option<String> {
    const TIMEOUT: Duration = Duration::from_secs(10);

    // reqwest is built on rustls without a crypto provider, as the updater's is; install ring's, as the updater does.
    if rustls::crypto::CryptoProvider::get_default().is_none() {
        let _ = rustls::crypto::ring::default_provider().install_default();
    }
    let client = reqwest::Client::builder().timeout(TIMEOUT).build().ok()?;
    let mut response = client.get(url).send().await.ok()?.error_for_status().ok()?;
    let mut body = Vec::new();
    while let Some(chunk) = response.chunk().await.ok()? {
        body.extend_from_slice(&chunk);
        if body.len() > AnkamaConfig::LIMIT {
            return None;
        }
    }
    String::from_utf8(body).ok()
}

/// Writes `bytes` to a temporary file beside `path`, then renames it over `path`, so that a client starting meanwhile
/// never reads half a file.
fn write(path: &Path, bytes: &[u8]) -> Result<(), RelayError> {
    let mut temporary = path.as_os_str().to_owned();
    temporary.push(".d3b-tmp");
    let temporary = PathBuf::from(temporary);
    let io = |source| RelayError::Io {
        path: path.to_path_buf(),
        source,
    };
    std::fs::write(&temporary, bytes).map_err(io)?;
    std::fs::rename(&temporary, path).map_err(io)
}

/// The `file:` URL of an absolute path: forward slashes, every byte but letters, digits and `-._~/:` percent-encoded.
fn file_url(path: &Path) -> String {
    let text = path.to_string_lossy().replace('\\', "/");
    let mut url = String::from(if text.starts_with('/') { "file://" } else { "file:///" });
    for byte in text.bytes() {
        if byte.is_ascii_alphanumeric() || b"-._~/:".contains(&byte) {
            url.push(char::from(byte));
        } else {
            // Writing to a `String` cannot fail.
            let _ = write!(url, "%{byte:02X}");
        }
    }
    url
}

#[cfg(test)]
mod tests {
    use super::*;

    const HTTPS: &str = "https://config.example.com/config.json";
    const CONFIG: &str =
        r#"{"chatHost":"chat.example.com:6337","connectionHosts":["production:connection.example.com:5555,443"]}"#;

    /// A client folder and a state folder under the system's temporary folder, removed when dropped.
    struct Folders {
        root: PathBuf,
        redirect: Redirect,
    }

    impl Folders {
        fn new(name: &str) -> Self {
            let root = std::env::temp_dir().join(format!("d3b-redirect-{name}-{}", std::process::id()));
            let _ = std::fs::remove_dir_all(&root);
            let client = root.join("client");
            std::fs::create_dir_all(client.join("Dofus_Data").join("StreamingAssets"))
                .unwrap_or_else(|error| panic!("{error}"));
            let folders = Self {
                redirect: Redirect::new(&client, root.join("state")),
                root,
            };
            folders.launcher_writes(HTTPS);
            folders
        }

        /// Writes the `version` file as Ankama's launcher does, naming `url`.
        fn launcher_writes(&self, url: &str) {
            let bytes = format!("Version=3.7.13.17\r\nBuildDate=2026-11-03T14:09:52+00:00\r\nConfigUrl={url}\r\n");
            std::fs::write(&self.redirect.version, bytes).unwrap_or_else(|error| panic!("{error}"));
        }

        fn version(&self) -> String {
            std::fs::read_to_string(&self.redirect.version).unwrap_or_else(|error| panic!("{error}"))
        }

        fn state(&self, name: &str) -> Option<String> {
            std::fs::read_to_string(self.redirect.state.join(name)).ok()
        }

        fn apply(&self, downloaded: Option<&str>) -> Result<ConnectionHost, RelayError> {
            let original = self.redirect.original_url()?;
            self.redirect.apply(&original, downloaded)
        }

        fn applied(&self) -> bool {
            self.redirect.is_applied().unwrap_or_else(|error| panic!("{error}"))
        }
    }

    impl Drop for Folders {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.root);
        }
    }

    #[test]
    fn points_the_client_at_the_copy_and_keeps_the_state() {
        let folders = Folders::new("apply");

        let host = folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(host.host, "connection.example.com");
        assert_eq!(host.ports, vec![5555, 443]);
        assert_eq!(
            folders.version(),
            format!(
                "Version=3.7.13.17\r\nBuildDate=2026-11-03T14:09:52+00:00\r\nConfigUrl={}\r\n",
                folders.redirect.copy_url()
            )
        );
        assert_eq!(folders.state("config-url").as_deref(), Some(HTTPS));
        assert_eq!(folders.state("ankama-config.json").as_deref(), Some(CONFIG));
        let copy = folders.state("config.json").unwrap_or_default();
        assert!(copy.contains("production:127.0.13.59:5555,443"), "{copy}");
        assert!(folders.applied());
    }

    #[test]
    fn after_a_crash_takes_the_kept_url() {
        let folders = Folders::new("crash");
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));

        let original = folders
            .redirect
            .original_url()
            .unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(original.url, HTTPS);
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));
        assert!(folders.applied());
        assert_eq!(folders.state("config-url").as_deref(), Some(HTTPS));
    }

    #[test]
    fn restores_ankamas_file_byte_for_byte() {
        let folders = Folders::new("restore");
        let before = folders.version();
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));

        let restored = folders.redirect.restore().unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(restored, Restored::Restored);
        assert_eq!(folders.version(), before);
        assert!(!folders.applied());
    }

    #[test]
    fn leaves_alone_a_line_that_is_no_longer_ours() {
        let folders = Folders::new("not-ours");
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));
        folders.launcher_writes("https://config.example.com/new.json");

        let restored = folders.redirect.restore().unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(restored, Restored::NotOurs);
        assert!(
            folders
                .version()
                .contains("ConfigUrl=https://config.example.com/new.json\r\n")
        );
    }

    #[test]
    fn applies_again_after_the_launcher_rewrote_the_file() {
        let folders = Folders::new("again");
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));
        folders.launcher_writes("https://config.example.com/new.json");

        assert!(!folders.applied());
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));

        assert!(folders.applied());
        assert_eq!(
            folders.state("config-url").as_deref(),
            Some("https://config.example.com/new.json")
        );
    }

    #[test]
    fn uses_the_kept_configuration_when_ankama_cannot_be_reached() {
        let folders = Folders::new("offline");
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));
        folders.launcher_writes(HTTPS);

        let host = folders.apply(None).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(host.host, "connection.example.com");
        assert!(folders.applied());
    }

    #[test]
    fn touches_nothing_without_any_configuration() {
        let folders = Folders::new("nothing");
        let before = folders.version();

        let error = folders.apply(None).err();

        assert!(matches!(error, Some(RelayError::NoAnkamaConfig)), "{error:?}");
        assert_eq!(folders.version(), before);
        assert!(!folders.redirect.state.exists());
    }

    #[test]
    fn touches_nothing_with_a_configuration_it_cannot_relay() {
        let folders = Folders::new("two-hosts");
        let before = folders.version();
        let two = r#"{"connectionHosts":["a:one.example.com:5555","b:two.example.com:5555"]}"#;

        let error = folders.apply(Some(two)).err();

        assert!(matches!(error, Some(RelayError::ConnectionHosts(2))), "{error:?}");
        assert_eq!(folders.version(), before);
        assert!(!folders.redirect.state.exists());
    }

    #[test]
    fn refuses_another_programs_redirect() {
        let folders = Folders::new("elsewhere");
        folders.launcher_writes("file:///C:/Users/player/AppData/Local/D3B/client/config.json");

        let error = folders.redirect.original_url().err();

        assert!(matches!(error, Some(RelayError::RedirectedElsewhere)), "{error:?}");
    }

    #[test]
    fn refuses_its_own_redirect_once_the_original_is_lost() {
        let folders = Folders::new("lost");
        folders.apply(Some(CONFIG)).unwrap_or_else(|error| panic!("{error}"));
        std::fs::remove_file(folders.redirect.state.join("config-url")).unwrap_or_else(|error| panic!("{error}"));

        let error = folders.redirect.original_url().err();

        assert!(matches!(error, Some(RelayError::OriginalLost)), "{error:?}");
    }

    #[test]
    fn refuses_a_version_file_rewritten_during_the_download() {
        let folders = Folders::new("changed");
        let original = folders
            .redirect
            .original_url()
            .unwrap_or_else(|error| panic!("{error}"));
        folders.launcher_writes("https://config.example.com/new.json");

        let error = folders.redirect.apply(&original, Some(CONFIG)).err();

        assert!(matches!(error, Some(RelayError::VersionChanged)), "{error:?}");
        assert!(folders.version().contains("new.json"));
    }

    #[test]
    fn a_missing_version_file_names_its_path() {
        let folders = Folders::new("missing");
        std::fs::remove_file(&folders.redirect.version).unwrap_or_else(|error| panic!("{error}"));

        let error = folders.redirect.original_url().err();

        assert!(
            matches!(&error, Some(RelayError::Io { path, .. }) if path.ends_with("version")),
            "{error:?}"
        );
    }

    #[test]
    fn refuses_a_version_file_too_large_without_reading_it_all() {
        let folders = Folders::new("large");
        std::fs::write(&folders.redirect.version, vec![b' '; 1 << 20]).unwrap_or_else(|error| panic!("{error}"));

        let error = folders.redirect.original_url().err();

        assert!(
            matches!(error, Some(RelayError::VersionTooLarge { size: 4097, .. })),
            "{error:?}"
        );
    }

    #[test]
    fn file_urls_encode_spaces_and_accents() {
        assert_eq!(
            file_url(Path::new(r"C:\Users\Jérôme Dupont\config.json")),
            "file:///C:/Users/J%C3%A9r%C3%B4me%20Dupont/config.json"
        );
        assert_eq!(
            file_url(Path::new("/home/player/config.json")),
            "file:///home/player/config.json"
        );
    }
}
