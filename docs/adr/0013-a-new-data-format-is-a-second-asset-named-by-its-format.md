# 0013. A new data format is a second asset of the build's release, named by its format

- Status: accepted
- Date: 2026-11-13
- Decided by: the owner
- Supersedes 0008

## Context

The character's tab names its breed and shows its head (the owner, 2026-11-13): the data folder gains `breeds.json`
and the breeds' heads, a change of shape, so a new format (0008's consequence). Format 1 of 3.7.13.17 is already
published as `client-3.7.13.17.tar.gz`, and a published asset is never replaced; 0008 names one archive per build.

## Decision

- **What 0008 decided holds, but the archive's name**: one folder per build from the .NET extractor, JSON with texts
  inline in five languages, the maps in 100 files, the tiles, a `.tar.gz` that every `tar` reads.
- **The archive is `client-<build>-format<n>.tar.gz`**: a build's data in a new format is a second asset of the same
  `client-<build>` release, beside the former ones, which stay for the versions of D3B that read them.
- **`.github/game-data.json` names the build and the format** (`"format": 2`); `data:fetch` and the release workflow
  download the asset of that format, and a Rust test keeps it equal to `data::FORMAT`.

## Rejected

- Keeping format 1 and reading the new tables only when the manifest lists them: two shapes under one number, which
  `docs/data.md`'s rule forbids.
- A new release per format: one release per build keeps a build's data in one place.

## Consequences

Until the new format's asset is published, `data:fetch` and a release find no data, and a `data/` of the former format
fails the test that reads it: the owner extracts and publishes first (`data` skill). Format 1's asset keeps its name.
