//! A file written whole or not at all (ADR 0015): the bytes go to a temporary file beside it, then a rename puts them
//! in its place, so that a crash, a full disk or a sync client reading meanwhile never sees half of the player's work.

use std::io;
use std::path::{Path, PathBuf};

/// The suffix of the temporary file beside the one being written; a crash may leave one, which readers skip and the
/// next write replaces.
pub(crate) const TEMPORARY: &str = ".d3b-tmp";

/// Writes `bytes` to `path` whole: a temporary file beside it, then renamed over it. On failure the file is as it was
/// and no temporary file is left.
pub(crate) fn write(path: &Path, bytes: &[u8]) -> io::Result<()> {
    let mut temporary = path.as_os_str().to_owned();
    temporary.push(TEMPORARY);
    let temporary = PathBuf::from(temporary);
    let written = std::fs::write(&temporary, bytes).and_then(|()| std::fs::rename(&temporary, path));
    if written.is_err() {
        let _ = std::fs::remove_file(&temporary);
    }
    written
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    #[test]
    fn a_file_is_written_whole_over_the_one_before() {
        let folder = Folder::new("atomic-over");
        let path = folder.path().join("blueprint.json");
        write(&path, b"first").unwrap_or_else(|error| panic!("{error}"));
        write(&path, b"second").unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(std::fs::read(&path).ok().as_deref(), Some(&b"second"[..]));
        assert_eq!(
            std::fs::read_dir(folder.path()).map(Iterator::count).ok(),
            Some(1),
            "no temporary file left"
        );
    }

    #[test]
    fn a_failed_write_leaves_the_file_as_it_was_and_no_temporary_file() {
        let folder = Folder::new("atomic-failed");
        // A folder where the file should be: the rename over it fails on every system.
        let path = folder.path().join("blueprint.json");
        std::fs::create_dir(&path).unwrap_or_else(|error| panic!("{error}"));
        assert!(write(&path, b"lost").is_err());
        assert!(path.is_dir());
        assert_eq!(
            std::fs::read_dir(folder.path()).map(Iterator::count).ok(),
            Some(1),
            "no temporary file left"
        );
    }
}
