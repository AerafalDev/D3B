---
name: new-tool
description: Add a tool to scripts/ - a file-based C# app - with everything the build and the IDE expect. Use when a task needs a new command that reads the client, its captures or its data.
argument-hint: "The tool's name and what it does"
---

A tool is a file-based app (D10). Read `scripts/client.cs` and `scripts/client/ClientCommand.cs` first: they are the model.

1. `scripts/<name>.cs`: first line `#!/usr/bin/env dotnet`, then `#:package <Name>` with no version (add the version to
   `Directory.Packages.props`), `#:project` for a `src/` project it needs, `#:property PublishAot=false` when a package is
   not AOT-compatible, and `#:include <name>/<Name>Command.cs`. A comment after the usings gives what it does, its usage
   and its exit codes.
2. Its code in the lowercase folder `scripts/<name>/`, namespace `D3B.Scripts.<Name>`, `internal static class`, one type
   per file. What several tools share lives in `scripts/unity/`.
3. List every new file under the matching `/scripts/` folders of `D3B.slnx`, so that the IDE shows it.
4. Add a `Build the <name> tool` step to `.github/workflows/ci.yml`, as the others: `dotnet build scripts/<name>.cs`.
5. Add the command to the README's tools section, and to `CLAUDE.md`'s commands only when a session needs it often.
6. A tool has no tests (D10). A tool that guards a rule is not a tool: see `.claude/hooks/`.

Done when `dotnet build scripts/<name>.cs`, `dotnet run --file scripts/<name>.cs -- --help` and
`dotnet format --verify-no-changes` pass.
