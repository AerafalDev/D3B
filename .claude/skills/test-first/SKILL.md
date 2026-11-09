---
name: test-first
description: Test-driven development in D3B - red then green, one behavior at a time, tests that survive a refactor. Use when adding a behavior or fixing a bug in src/.
---

A test is red before the code that makes it green, and it fails for the reason the behavior is missing.

## The loop

1. Name the behavior as a sentence the owner could read: that is the test's name, `AModuleTypeIsBuiltOnce`.
2. Write that one test, through the public type, in the test project that mirrors the code: `tests/D3B.<Project>.Tests`.
3. Run it and watch it fail for the right reason:
   `dotnet test --project tests/D3B.Engine.Tests --filter-method "*AModuleTypeIsBuiltOnce"`. `--filter-class` takes a class,
   `--list-tests` lists them, `--no-build` skips the build.
4. Write the least code that turns it green; run the project's tests.
5. Next behavior, next test. One slice at a time: writing every test first tests an imagined shape.

A bug fix starts with a test that fails on the bug.

## What a good test is

- It checks a behavior through the type's public surface, so that it survives a refactor. It breaks when behavior breaks.
- Its expected value comes from outside the code under test: a literal, a worked example, a decision's number, a captured
  fight. A test that recomputes the answer the way the code does passes by construction.
- It names the failure case too: the input that must be refused, the state that must not happen.
- `CsCheck` states a property that holds over many inputs, where examples would be a guess.
- xUnit v3 on Microsoft.Testing.Platform; test methods are PascalCase sentences without underscores (CA1707).

## The tests as the repository writes them

Counted in `tests/` on 2026-11-09: 1,700 tests, almost all `[Fact]`, a `[Theory]` where one rule has many inputs.

- One test project per project with logic, mirroring its folders (`tests/D3B.Bot.Tests/Harvesting` for
  `src/D3B.Bot/Harvesting`). A helper that several tests share lives in the test project, beside them: `TestHosts`,
  `TestGameData`, `TestStore`, `Waiting` in `D3B.App.Tests`; `TestInstall` in `D3B.Bot.Tests`.
- Assertions are xUnit's own (`Assert.Equal`, `Assert.Null`, `Assert.Single`); no assertion library and no mocking library
  is used. A double is a small class of the test project.
- An async test passes `TestContext.Current.CancellationToken` to what it awaits.
- Time under test is a `FakeTimeProvider` wherever the code takes a `TimeProvider`; waiting for something that runs uses the test project's `Waiting` helper, not a sleep.
- The engine's rules are tested on data built by the test, or on fights as the captures measured them (`D3B.Engine.Tests`);
  `CsCheck` states a property there.
- A view is tested headless: the `studio-ui` skill says how.
- Test methods are PascalCase sentences that state the behavior, `TheRunBlockShowsOnlyTheButtonsTheChosenCharactersStateAllows`,
  and a file holds one class, named for what it tests, with `Tests` after it.

## Where tests do not go

`scripts/` tools have none (D10). Behavior that only shows in the game cannot be tested: say how it was checked, on which
client build, in the pull request. A guard of a rule (`.claude/hooks/`) is tested in `tests/D3B.Hooks.Tests`.

Done when the new test was seen red, then green, and `dotnet test` passes.
