---
name: read-client
description: Read the official Dofus 3 client, its data or a capture of its protocol - which tool to run, and what never reaches the repository. Use before inspecting the installed client, a capture, a schema or a mapping.
---

Start from what is already measured: `docs/client.md` is long, so grep its headings for the area, then read that section.

| To learn | Run |
| --- | --- |
| What the installed client holds | `dotnet run --file scripts/inspect.cs -- summary`; `--help` lists the commands |
| What a capture holds, by structure | `dotnet run --file scripts/capture.cs -- [paths]`; `--frames` shows each frame's protobuf shape; `--schema <file>` reads it field by field; `--messages --mapping <file>` lists the messages by name |
| What the engine foresees of captured fights | `dotnet run --file scripts/fights.cs`; `--turns` replays the AI's turns |
| The build's protocol | `scripts/schema.cs`, then `scripts/mapping.cs` |

The rules, all of D2, D3, D10, D11 and D24:

- Nothing extracted from the client is committed: no asset, no data, no dump, no schema, no mapping, no pack, no `.proto`
  text written from a schema. The write guard hook refuses most of them.
- A capture holds the account's login ticket. Describe it with `scripts/capture.cs`; never print or quote its content.
- What you quote from the client or from `vendor/` while studying stays under `artifacts/studies/`, which Git ignores. A
  finding that goes to `docs/client.md` is a measured fact in our own words, with where it was measured.
- `vendor/` is read for ideas, never copied (D3). Decompiling with the `rea` MCP server, when it is connected, follows the same rules.

Done when the answer says which tool or file it comes from, and nothing of the client's content is in a tracked file.
