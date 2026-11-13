# Testing on every platform

The owner measures D3B on Windows x64. macOS (Intel and Apple Silicon), Linux (x64 and ARM64) and Windows ARM64 are
measured by volunteers from the [Discord server](https://discord.com/invite/6A6Vw9Mn24), who report each result through
the **Test report** issue form. A platform no tester has measured stays under "Not measured" in `docs/now.md`.

## How a round goes

1. A release is published (`release` skill). Claude posts the round's checklist below, for that version, as text the
   owner pastes on the Discord.
2. Testers install the version from the release page and fill one **Test report** per platform.
3. Claude reads the reports (`gh issue list --label test-report`), moves what passed to "Measured" in `docs/now.md`
   with the tester's platform, and turns what failed into bugs (`diagnose`).

## The checklist

Every round checks the app itself; a round may add the feature it ships, from that feature's in-game checklist.

1. **Install**: the installer (or the AppImage, `.deb`, `.rpm`) installs without error, without administrator rights
   on Windows.
2. **Start**: the window opens, dark, in the system's language (French or English).
3. **Status bar**: it shows `D3B <version>` on the left and the game data on the right (`Client <build>`, or
   `No game data` and why in its hover); after the version, the relay: "Relay not measured on this system", grey,
   on macOS and Linux.
4. **Close**: the close button quits the app.
5. **One instance**: launching D3B again while it runs brings the running window back.
6. **Update** (from the second release on): with the previous version installed, the new one is downloaded at start,
   the status bar's right offers to restart into it, and choosing it starts the new version; on another update,
   closing D3B installs the new version and the next start is that version.

The warnings of unsigned installers are expected (README, *The installers are not signed yet*).
