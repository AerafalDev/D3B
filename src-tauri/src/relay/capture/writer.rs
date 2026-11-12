//! The writer of captures: the copy hands each read to a [`Recorder`] without waiting, and a blocking task writes
//! them to the file, so that a slow disk can never slow the game.

use std::fs::File;
use std::io::{BufWriter, Write};
use std::path::PathBuf;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::{SystemTime, UNIX_EPOCH};

use tokio::sync::mpsc;
use tokio::task::JoinHandle;
use tokio::time::Instant;

use super::super::game_connection::Side;
use super::super::handover::GameServer;
use super::super::{ADDRESS, GAME_PORT, wire};
use super::{FORMAT, FROM_CLIENT, FROM_SERVER, MAGIC};

/// How many reads may wait for the disk before the capture is cut: about 64 MiB of the largest reads.
const WAITING: usize = 1024;

/// Where captures go and the build they name: the folder of `D3B_RECORD` and the installed client's build.
#[derive(Debug, Clone)]
pub(crate) struct Recording {
    pub(crate) folder: PathBuf,
    pub(crate) build: String,
}

/// The capture of one game connection, as it goes.
#[derive(Debug)]
pub(crate) struct Recorder {
    reads: mpsc::Sender<(u8, u64, Vec<u8>)>,
    start: Instant,
    /// Set once a read could not wait: nothing more is recorded, and the file is named as cut.
    cut: Arc<AtomicBool>,
    writer: JoinHandle<()>,
}

impl Recording {
    /// Starts the capture of the `number`th game connection, to `server`, begun at `started`: a file
    /// `<build>-<Unix milliseconds>-<number>.d3bcap` in the folder, `-cut` added to its name if the writer fell
    /// behind. A folder or a file that cannot be written gives no capture, and the game goes on.
    pub(crate) fn start(&self, server: &GameServer, started: SystemTime, number: u64) -> Recorder {
        let millis = started.duration_since(UNIX_EPOCH).unwrap_or_default().as_millis();
        let name = format!("{}-{millis}-{number}", self.build);
        let mut header = Vec::new();
        header.extend_from_slice(MAGIC);
        header.extend_from_slice(&FORMAT.to_le_bytes());
        header.extend_from_slice(&i64::try_from(millis).unwrap_or_default().to_le_bytes());
        let listener = format!("{ADDRESS}:{GAME_PORT}");
        let to = format!("{}:{}", server.host, server.port);
        for text in [
            self.build.as_str(),
            listener.as_str(),
            server.host.as_str(),
            to.as_str(),
        ] {
            wire::write_varint(text.len() as u64, &mut header);
            header.extend_from_slice(text.as_bytes());
        }
        Recorder::new(self.folder.clone(), name, header, WAITING)
    }
}

impl Recorder {
    fn new(folder: PathBuf, name: String, header: Vec<u8>, waiting: usize) -> Self {
        let (reads, received) = mpsc::channel(waiting);
        let cut = Arc::new(AtomicBool::new(false));
        let writer = {
            let cut = Arc::clone(&cut);
            tokio::task::spawn_blocking(move || write(&folder, &name, &header, received, &cut))
        };
        Self {
            reads,
            start: Instant::now(),
            cut,
            writer,
        }
    }

    /// Records the bytes one read of `side` gave, unless the capture is cut; never waits.
    pub(crate) fn record(&self, side: Side, bytes: &[u8]) {
        if self.cut.load(Ordering::Relaxed) {
            return;
        }
        let kind = match side {
            Side::Client => FROM_CLIENT,
            Side::Server => FROM_SERVER,
        };
        let micros = u64::try_from(self.start.elapsed().as_micros()).unwrap_or(u64::MAX);
        if let Err(mpsc::error::TrySendError::Full(_)) = self.reads.try_send((kind, micros, bytes.to_vec())) {
            self.cut.store(true, Ordering::Relaxed);
        }
    }

    /// Ends the capture once the connection ended, its file whole up to its last record.
    pub(crate) async fn finish(self) {
        drop(self.reads);
        let _ = self.writer.await;
    }
}

/// Writes the header, then every read received until the connection ends; then names the file as cut if it was.
fn write(
    folder: &std::path::Path,
    name: &str,
    header: &[u8],
    mut received: mpsc::Receiver<(u8, u64, Vec<u8>)>,
    cut: &AtomicBool,
) {
    let path = folder.join(format!("{name}.d3bcap"));
    let opened = std::fs::create_dir_all(folder).and_then(|()| File::create_new(&path));
    let Ok(file) = opened else {
        // No capture: whatever comes is dropped as it comes.
        cut.store(true, Ordering::Relaxed);
        return;
    };
    let mut file = BufWriter::new(file);
    let mut written = file.write_all(header).is_ok();
    let mut record = Vec::new();
    while let Some((kind, micros, bytes)) = received.blocking_recv() {
        if !written {
            continue;
        }
        record.clear();
        record.push(kind);
        wire::write_varint(micros, &mut record);
        wire::write_varint(bytes.len() as u64, &mut record);
        record.extend_from_slice(&bytes);
        written = file.write_all(&record).is_ok();
        if !written {
            cut.store(true, Ordering::Relaxed);
        }
    }
    if file.flush().is_err() {
        cut.store(true, Ordering::Relaxed);
    }
    drop(file);
    if cut.load(Ordering::Relaxed) {
        let _ = std::fs::rename(&path, folder.join(format!("{name}-cut.d3bcap")));
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use super::super::{Record, read};
    use super::*;

    /// A folder of its own under the system's temporary folder, emptied first.
    fn folder(name: &str) -> PathBuf {
        let folder = std::env::temp_dir().join(format!("d3b-capture-{}-{name}", std::process::id()));
        let _ = std::fs::remove_dir_all(&folder);
        folder
    }

    fn files(folder: &std::path::Path) -> Vec<String> {
        let mut names: Vec<String> = std::fs::read_dir(folder)
            .into_iter()
            .flatten()
            .flatten()
            .map(|entry| entry.file_name().to_string_lossy().into_owned())
            .collect();
        names.sort();
        names
    }

    fn recording(folder: PathBuf) -> Recording {
        Recording {
            folder,
            build: "3.7.13.17".into(),
        }
    }

    fn server() -> GameServer {
        GameServer {
            host: "game.example.com".into(),
            port: 5555,
        }
    }

    #[tokio::test]
    async fn records_each_read_in_order_named_with_the_build() {
        let folder = folder("order");
        let started = UNIX_EPOCH + Duration::from_millis(1_762_900_000_123);

        let recorder = recording(folder.clone()).start(&server(), started, 7);
        recorder.record(Side::Client, b"ticket");
        recorder.record(Side::Server, b"accepted");
        recorder.record(Side::Client, b"");
        recorder.finish().await;

        assert_eq!(files(&folder), ["3.7.13.17-1762900000123-7.d3bcap"]);
        let bytes = std::fs::read(folder.join("3.7.13.17-1762900000123-7.d3bcap")).unwrap_or_default();
        let capture = read(&bytes).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(capture.build, "3.7.13.17");
        assert_eq!(
            capture.records,
            [
                Record::FromClient(b"ticket".to_vec()),
                Record::FromServer(b"accepted".to_vec()),
                Record::FromClient(Vec::new()),
            ]
        );
        let _ = std::fs::remove_dir_all(&folder);
    }

    #[tokio::test]
    async fn writes_the_header_of_format_2() {
        let folder = folder("header");
        let started = UNIX_EPOCH + Duration::from_millis(258);

        recording(folder.clone()).start(&server(), started, 1).finish().await;

        let bytes = std::fs::read(folder.join("3.7.13.17-258-1.d3bcap")).unwrap_or_default();
        let _ = std::fs::remove_dir_all(&folder);
        let mut expected = b"D3B-CAPT".to_vec();
        expected.extend_from_slice(&2_i32.to_le_bytes());
        expected.extend_from_slice(&258_i64.to_le_bytes());
        for text in [
            "3.7.13.17",
            "127.0.13.59:5556",
            "game.example.com",
            "game.example.com:5555",
        ] {
            expected.push(u8::try_from(text.len()).unwrap_or_default());
            expected.extend_from_slice(text.as_bytes());
        }
        assert_eq!(bytes, expected);
    }

    #[tokio::test]
    async fn never_writes_over_an_earlier_capture() {
        let folder = folder("taken");
        std::fs::create_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        let path = folder.join("3.7.13.17-0-1.d3bcap");
        std::fs::write(&path, b"earlier").unwrap_or_else(|error| panic!("{error}"));

        let recorder = recording(folder.clone()).start(&server(), UNIX_EPOCH, 1);
        let cut = Arc::clone(&recorder.cut);
        recorder.record(Side::Client, b"later");
        recorder.finish().await;

        let kept = std::fs::read(&path).unwrap_or_default();
        let _ = std::fs::remove_dir_all(&folder);
        assert_eq!(kept, b"earlier");
        assert!(cut.load(Ordering::Relaxed), "the reads kept being taken");
    }

    #[tokio::test]
    async fn a_read_that_cannot_wait_cuts_the_capture() {
        // A disk that takes nothing: the writer's end is held here, never read.
        let (reads, mut received) = mpsc::channel(1);
        let recorder = Recorder {
            reads,
            start: Instant::now(),
            cut: Arc::default(),
            writer: tokio::spawn(async {}),
        };

        recorder.record(Side::Client, b"one");
        let before = recorder.cut.load(Ordering::Relaxed);
        recorder.record(Side::Client, b"two");
        recorder.record(Side::Client, b"three");
        let cut = recorder.cut.load(Ordering::Relaxed);
        recorder.finish().await;

        assert!(!before, "one read may wait");
        assert!(cut, "a second read waited where one may");
        assert_eq!(received.recv().await.map(|(_, _, bytes)| bytes), Some(b"one".to_vec()));
        assert!(received.recv().await.is_none(), "a read came after the cut");
    }

    #[tokio::test]
    async fn names_a_cut_capture_as_cut_and_keeps_its_whole_records() {
        let folder = folder("named-cut");
        let recorder = recording(folder.clone()).start(&server(), UNIX_EPOCH, 1);

        recorder.record(Side::Client, b"before");
        recorder.cut.store(true, Ordering::Relaxed);
        recorder.record(Side::Client, b"after");
        recorder.finish().await;

        assert_eq!(files(&folder), ["3.7.13.17-0-1-cut.d3bcap"]);
        let bytes = std::fs::read(folder.join("3.7.13.17-0-1-cut.d3bcap")).unwrap_or_default();
        let capture = read(&bytes).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(capture.records, [Record::FromClient(b"before".to_vec())]);
        let _ = std::fs::remove_dir_all(&folder);
    }

    #[tokio::test]
    async fn a_folder_that_cannot_be_made_gives_no_capture() {
        let folder = folder("blocked");
        std::fs::write(&folder, b"in the way").unwrap_or_else(|error| panic!("{error}"));

        let recorder = recording(folder.join("inside")).start(&server(), UNIX_EPOCH, 1);
        let cut = Arc::clone(&recorder.cut);
        recorder.record(Side::Server, b"lost");
        recorder.finish().await;

        assert!(folder.is_file(), "the file in the way was touched");
        assert!(cut.load(Ordering::Relaxed), "the reads kept being taken");
        let _ = std::fs::remove_file(&folder);
    }
}
