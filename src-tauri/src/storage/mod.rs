//! The player's files (`docs/bot.md`, "Storage"; ADR 0015): one JSON file per object in `Documents/D3B`, written whole
//! by a temporary file and a rename, and a file that does not read kept untouched and reported, never written over.
//! The blueprints come first; zones, routes, characters and history join them as their PRs come.

mod atomic;
mod character;
mod store;

pub(crate) use character::{Played, SCHEMA as CHARACTER_SCHEMA, key_of};
pub(crate) use store::{StorageError, Store, Why};

#[cfg(test)]
pub(crate) mod tests {
    use std::path::{Path, PathBuf};

    /// A folder under the system's temporary folder, removed when dropped.
    pub(crate) struct Folder(PathBuf);

    impl Folder {
        pub(crate) fn new(name: &str) -> Self {
            let root = std::env::temp_dir().join(format!("d3b-storage-{name}-{}", std::process::id()));
            let _ = std::fs::remove_dir_all(&root);
            std::fs::create_dir_all(&root).unwrap_or_else(|error| panic!("{error}"));
            Self(root)
        }

        pub(crate) fn path(&self) -> &Path {
            &self.0
        }
    }

    impl Drop for Folder {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }
}
