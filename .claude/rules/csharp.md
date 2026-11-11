---
paths:
  - "tools/extractor/**"
---

# C# (the extractor)

- **.NET 10**, pinned in `tools/extractor/global.json`; warnings are errors, nullable on, the style enforced by the
  build (`tools/extractor/.editorconfig`). Check with `bun run check:extractor` (`dotnet format --verify-no-changes`
  and `dotnet test`, from `tools/extractor`).
- **Style**: methods, constructors, operators and local functions have block bodies, never `=>` (properties may);
  braces always; no primary constructors; one type per file, one folder per namespace; members in the order
  constants, static fields, fields, properties, constructors, static methods, methods, nested types.
- **Tests** (xUnit v3, in `tests/`) are PascalCase sentences without underscores, such as
  `APathOutsideTheFolderIsRefused`. Most need no client: they read hand-written bytes and fixtures. Those that measure
  the installed client (`Installed.Client()`) run wherever one is installed and are skipped elsewhere, as in CI. A fixture the app
  also reads (`tests/fixtures/`) is the contract between the two: the C# test writes it byte for byte, the Rust test
  parses it.
- **What the client holds** is read as it is and checked: an extraction that meets something the docs do not describe
  fails with what it met, rather than writing a guess. What the bot derives from it is computed in Rust.
- **Packages** are versioned in `Directory.Packages.props`; a new one is the owner's choice (`dependency` skill).
