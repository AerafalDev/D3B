# 0001. Rewrite D3B in Rust, Tauri 2, React 19 and TypeScript, on Bun and Vite

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

The C# version (.NET 10, Avalonia) runs on Windows only, and its interface was hard to make ergonomic. The owner wants
a cleaner, more complete and more ergonomic app on Windows, macOS and Linux, with a modern editor-like interface.

## Decision

- **Back end in Rust**, in the Tauri 2 shell: one binary per OS, a small installer, a webview for the interface.
- **Front end in React 19 and TypeScript 7**, with shadcn (radix-nova style) on Tailwind 4, and the owner's editor
  libraries: React Flow (+ React Flow UI, elkjs), zustand and zundo, dockview, react-arborist, Monaco, xterm, dnd-kit,
  react-hotkeys-hook, react-hook-form and zod.
- **Bun** installs and runs scripts (`bun.lock`, exact versions); **Vite** serves and builds the front end, the
  toolchain Tauri, shadcn and Tailwind support first.
- **Biome** formats and lints the TypeScript; rustfmt and clippy (pedantic) the Rust. **Vitest** tests the front end.
- The work happens on branch `next`, started from `main`, which it replaces once milestone 1 is measured.

## Rejected

- Keeping .NET with another UI (MAUI, Avalonia): Windows-first ecosystem, and the owner wants Rust.
- Electron: a heavier runtime than Tauri for no gain here.
- Bun alone (`Bun.build`, no Vite): more glue for Tauri's dev server and Tailwind.
- ESLint and Prettier: two slower tools for what Biome does alone.

## Consequences

Nothing of the C# code is reused as is; its measured docs and lessons are (`docs/lessons.md`). The extraction tools
stay in .NET (ADR 0003).
