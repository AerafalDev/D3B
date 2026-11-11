---
paths:
  - "src/**/*.tsx"
  - "src/index.css"
  - "components.json"
---

# React 19, shadcn and the interface

- **Components are functions**, one exported component per file, named after it (`status-bar.tsx` exports
  `StatusBar`); typed props; no default export, no component defined inside another.
- **shadcn** (`radix-nova` style, Lucide icons, Tailwind 4 with the theme's CSS variables). Add a component with
  `bunx --bun shadcn@latest add <name>`; it lands in `src/components/ui/`, which we do not edit by hand beyond the
  smallest fix the typecheck needs. React Flow UI's components are in `src/components/flow/`.
- **Styling**: Tailwind classes through `cn()`; no inline style objects, no CSS-in-JS. Dark theme first; both themes
  must stay readable.
- **State**: `zustand` stores per domain (with `zundo` where undo exists), read through selectors; server state from
  the back end arrives by typed events into a store. No React state for what changes per frame: use a ref and
  `requestAnimationFrame`.
- **Editor pieces**: `dockview-react` for the docked layout, `@xyflow/react` (+ `elkjs` for layout) for node graphs,
  `react-arborist` for trees, Monaco for text, xterm for logs, `@dnd-kit` for drag and drop, `react-hotkeys-hook` for
  shortcuts, `react-hook-form` + `zod` for forms. Load the heavy ones (Monaco, xterm, React Flow) with `React.lazy`.
- **Every visible string** goes through `useTranslation()`; keys are typed from `src/i18n/locales/en.json`, and
  `fr.json` has the same keys (a test checks it). No string concatenation for sentences: interpolation.
- **Accessible**: real buttons and labels, keyboard focus visible, every action reachable by keyboard; shortcuts are
  listed in the command palette.
- `react-best-practices` holds the general performance rules; this page wins where they differ.
