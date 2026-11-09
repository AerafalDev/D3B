---
name: csharp
description: How C# is written in D3B - types, null, async, time, logging, collections, UI code - read from the source. Use before writing or changing C# in src/ or tests/.
---

`CLAUDE.md` gives the style rules and `.editorconfig` makes the build enforce them (warnings are errors). This skill is what
the build does not say. Each rule below was counted in `src/` on 2026-11-09 (914 files); when code you read breaks one, follow
the rule and do not copy the code.

## Types

- A class is `sealed` unless it is made to be inherited: 306 of them are. A type that leaves nothing is `internal`.
- Data is a `record`, a `record struct` or a `readonly struct` (244, 39, 46), set with `init` or `required` properties
  (571, 67), not with a long constructor. Primary constructors are not used (`CLAUDE.md`).
- Public types and members of `src/` carry XML documentation: the build requires it. A comment says why a constraint
  exists, never what the line does.
- Null is checked at the edge of a public member with `ArgumentNullException.ThrowIfNull` (672 uses), and `Nullable` is on.

## Async

- A `CancellationToken` goes through every async call chain (245 uses) and ends at the call that can stop.
- `ConfigureAwait(false)` on every `await` in `D3B.Bot`, `D3B.Studio` and `D3B.Storage` (235 of 236, 34 of 34, 99 of 99). Not in
  `D3B.App`, whose code goes back to the UI thread, nor in `D3B.Network`.
- `async void` only for an Avalonia event handler, in a view's code-behind or a control of `D3B.App` (all 8 are there).
- A bot's state belongs to its actor: one `Channel<T>` inbox read by one loop, no lock on that state (D9; `docs/architecture.md`,
  "The actor"). Read that section before adding a `lock` (43 exist).

## Time and randomness

- Time is `TimeProvider`: there is no `DateTime.Now` or `DateTime.UtcNow` in `src/`, and every delay goes through it (D151,
  D152), so that tests run on `FakeTimeProvider`. A behavior's waits read `BotDelays`.

## Logging

- `ILogger<T>` with source-generated `[LoggerMessage]` partial methods (92 uses), not an interpolated string in a log call.

## Collections and speed

- Store `ImmutableArray<T>` (179) and expose `IReadOnlyList<T>` (393); build a lookup once into a `FrozenDictionary` (40).
- `Span<T>` and `stackalloc` live in the network and protocol hot paths, where speed matters. Elsewhere, write the
  clear code first; a speed claim comes with numbers (a `Stopwatch` harness in a script, or the replayed captures).
- A regular expression is a `[GeneratedRegex]`.

## The studio (`D3B.App`)

- View models use CommunityToolkit.Mvvm: `[ObservableProperty]` and `[RelayCommand]` on `partial` classes (463 and 192 uses).
  Views are `.axaml` with a thin code-behind; texts go through the `.resx` files, in French and English (D14).

## Generated code

- The classes Grpc.Tools makes from the `.proto` files and the EF Core migrations are generated: change the input and
  regenerate. A `#pragma warning disable` belongs only to generated code (13 of them are in migrations' `Designer.cs`).

Done when `dotnet build` is green, the new code follows the sections it touches, and `dotnet test` passes.
