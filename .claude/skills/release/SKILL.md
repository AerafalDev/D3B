---
name: release
description: Ship a D3B release through release-please's PR, on the owner's go, and follow the release workflow until every installer is attached. Use when the owner asks for a release.
disable-model-invocation: true
---

1. **The release PR**: release-please keeps one open against `next` (`gh pr list --label "autorelease: pending"`).
   Read its changelog: a commit whose header says nothing is reworded in the notes, never hidden. The version comes
   from the commits (`feat` raises the minor, `fix` the patch, `!` the major while we are past 1.0).
2. **The data**: `data-build.txt` names the client build to pack; check that its `client-<build>` release exists
   (`gh release view client-<build>`).
3. **Ask the owner** for the go, with the version, the changelog and the data build. Only then merge the release PR
   (`gh pr merge <n> --squash`).
4. **Follow** the release workflow (`gh run watch`): six targets, the installers and `latest.json` attached to the
   release. A failed target is fixed on a branch and released again as a patch, never patched by hand on the
   release.
5. **Report**: the release's URL and what it holds, and what the owner should measure on each OS they have.
