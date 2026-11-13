//! A file written whole or not at all (ADR 0015): the bytes go to a temporary file beside it, flushed to the disk, then
//! a rename puts them in its place, so that a crash of the app or of the system, a full disk or a sync client reading
//! meanwhile never sees half of the player's work. One write at a time per file: the caller holds it (`Store`).

use std::io::{self, Write};
use std::path::{Path, PathBuf};

/// The suffix of the temporary file beside the one being written; a crash may leave one, which readers skip and the
/// next write of that file replaces.
pub(crate) const TEMPORARY: &str = ".d3b-tmp";

/// Writes `bytes` to `path` whole: a temporary file beside it, flushed to the disk, then renamed over it. On failure
/// the file is as it was and no temporary file is left.
pub(crate) fn write(path: &Path, bytes: &[u8]) -> io::Result<()> {
    let mut temporary = path.as_os_str().to_owned();
    temporary.push(TEMPORARY);
    let temporary = PathBuf::from(temporary);
    let written = flushed(&temporary, bytes).and_then(|()| std::fs::rename(&temporary, path));
    if written.is_err() {
        let _ = std::fs::remove_file(&temporary);
        return written;
    }
    // Where the system allows it, the rename itself is flushed too, so a power cut cannot bring back the file before.
    #[cfg(unix)]
    if let Some(folder) = path.parent() {
        std::fs::File::open(folder)?.sync_all()?;
    }
    Ok(())
}

/// Writes `bytes` to a new `path`, then waits for the disk to hold them: without it, a power cut after the rename
/// could leave an empty file in place of the player's work (`sync_all` is a full flush on macOS too).
fn flushed(path: &Path, bytes: &[u8]) -> io::Result<()> {
    let mut file = std::fs::File::create(path)?;
    file.write_all(bytes)?;
    file.sync_all()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn count(folder: &Folder) -> Option<usize> {
        std::fs::read_dir(folder.path()).map(Iterator::count).ok()
    }

    #[test]
    fn a_file_is_written_whole_over_the_one_before_and_a_crashs_leftover() {
        let folder = Folder::new("atomic-over");
        let path = folder.path().join("blueprint.json");
        std::fs::write(folder.path().join("blueprint.json.d3b-tmp"), b"half").unwrap_or_else(|error| panic!("{error}"));
        write(&path, b"first").unwrap_or_else(|error| panic!("{error}"));
        write(&path, b"second").unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(std::fs::read(&path).ok().as_deref(), Some(&b"second"[..]));
        assert_eq!(count(&folder), Some(1), "no temporary file left");
    }

    #[test]
    fn a_failed_write_leaves_the_file_as_it_was_and_no_temporary_file() {
        let folder = Folder::new("atomic-failed");
        // A folder where the file should be: the rename over it fails on every system.
        let path = folder.path().join("blueprint.json");
        std::fs::create_dir(&path).unwrap_or_else(|error| panic!("{error}"));
        assert!(write(&path, b"lost").is_err());
        assert!(path.is_dir());
        assert_eq!(count(&folder), Some(1), "no temporary file left");
    }
}
