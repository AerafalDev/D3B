# Docs, the README for players (D469)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The repository's
public face is [D204](d176-d208-step-9.md#d204-the-repositorys-public-face-its-community-files-and-the-tool-manifest-in-config).

## D469: The README written for players, building and maintaining D3B in docs/development.md

*2026-11-20, a session of its own between the app's 0.9.3 and 0.9.4, the owner's ask ("plus clair, plus professionnel,
plus simple, et surtout à jour"), merged by the owner as pull request #345. Supersedes D204's layout of the README.*

- **Found**: the README had grown to about 270 lines, its players' part buried among the host's options, the tools of
  `scripts/`, packs, mappings and the installer's build, and parts of it were stale.
- **The README** is for players: what D3B is and does, installing it from the latest release, getting started, where to
  get help, what it never does (D2), the license. It keeps the build, test and run commands, which stay in step with
  `CLAUDE.md` (D204).
- **`docs/development.md`** holds the rest: building and running from source, the host's options (with
  `--Updates:Source`), the tools of `scripts/`, packs, mappings and releases, how D3B works. `CLAUDE.md`, `CONTRIBUTING.md`
  and the `/new-tool` skill point there.
- **Every fact checked against the code**: the installer, the given profiles, the delays and banking bounds, the host's
  configuration keys, the release `client-3.7.14.18`. No Follow profile is given, the activity having no preset, so the
  README says to make one.
- **Rejected.**
  - Trimming the README in place: one page for players and contributors keeps one of them lost.
  - Moving the commands out of the README too: D204's rule that the README restates `CLAUDE.md`'s commands for users holds.
