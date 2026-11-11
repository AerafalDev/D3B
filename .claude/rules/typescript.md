---
paths:
  - "**/*.ts"
  - "**/*.tsx"
---

# TypeScript 7 and Bun

- **TypeScript 7 checks** (`bun run typecheck`), Vite builds, Bun runs scripts and installs (`bun.lock` committed,
  exact versions from `bunfig.toml`). No npm, no pnpm.
- **Strict** (`tsconfig.json`): `noUncheckedIndexedAccess` (an index may be `undefined`: check it),
  `noPropertyAccessFromIndexSignature` (`env["NAME"]`), `verbatimModuleSyntax` (`import type`), imports carry their
  `.ts`/`.tsx` extension, `@/` is `src/`.
- No `any`, no `@ts-ignore`, no non-null assertion `!` (Biome refuses them); `@ts-expect-error` only in a test, with
  why. Narrow with a check, or `as` with a comment saying why it holds.
- Errors in normal use are discriminated unions (`{ ok: true, value } | { ok: false, error }`); exceptions are bugs.
- File names in kebab-case; one exported subject per file; TSDoc on exports saying what and why.
- **Scripts** (`scripts/*.ts`) use Node's modules (`node:fs`, `node:child_process`) so they run under Bun and in CI;
  one script, one job, a short output, exit 1 on failure and 2 when it cannot run. Inputs never reach a shell string:
  pass them as arguments.
