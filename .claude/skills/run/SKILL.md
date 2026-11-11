---
name: run
description: Launch D3B (the desktop app, or its front end alone in the browser pane) and show a change working with screenshots. Use when asked to run, start or screenshot the app, or to confirm a change works in the real app and not only in tests.
---

1. **Which**: the front end alone (`bun run dev`, port 1420) for an interface change that needs no back end; it runs
   in the browser pane through `.claude/launch.json`'s `web` entry, and commands answer as "Browser preview". The
   desktop app (`bun run app`) for anything that calls the back end, the relay or the packed data.
2. **The front end**: start `web` with the preview tools, open the page, and use the screenshot and the
   accessibility tree; check both themes and both languages when the change shows text.
3. **The desktop app**: run `bun run app` in the background and read its log (the Tauri log plugin writes to the
   terminal in debug). Its window cannot be driven from here: ask the owner for a screenshot of what you need, or
   check the behaviour through a test.
4. **Show it**: screenshots and the relevant log lines; say what was not exercised. Stop what you started.
5. A check worth keeping becomes a test (`tdd`), not a one-off.
